using DataAccessLayer.Models;
using DataAccessLayer.UnitOfWork;
using DocumentParser.Ocr;
using DocumentParser.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

/// <summary>
/// Background processing for a queued document:
/// Loads document metadata → parses PDF/DOCX → chunks text → embeds chunks →
/// persists chunks with their embeddings to PostgreSQL → updates status to completed/failed.
/// </summary>
public sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly Prn222Context _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageService _storageService;
    private readonly IFixedSizeChunkingService _chunkingService;
    private readonly IChunkingSettingsService _chunkingSettingsService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentProcessingNotifier _notifier;
    private readonly OcrOptions _ocrOptions;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(
        Prn222Context context,
        IUnitOfWork unitOfWork,
        IStorageService storageService,
        IFixedSizeChunkingService chunkingService,
        IChunkingSettingsService chunkingSettingsService,
        IEmbeddingService embeddingService,
        IDocumentProcessingNotifier notifier,
        IOptions<OcrOptions> ocrOptions,
        ILogger<DocumentProcessor> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _storageService = storageService;
        _chunkingService = chunkingService;
        _chunkingSettingsService = chunkingSettingsService;
        _embeddingService = embeddingService;
        _notifier = notifier;
        _ocrOptions = ocrOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        ProcessingJob? job = await _context.ProcessingJobs
            .Where(j => j.DocumentId == documentId)
            .OrderByDescending(j => j.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        try
        {
            if (job is not null)
            {
                job.JobStatus = "processing";
                job.StartedAt = UnspecifiedNow();
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await _notifier.NotifyProgressAsync(documentId, 5, "Đang xử lý", cancellationToken);

            // 1. Fetch document metadata
            var document = await _unitOfWork.Documents.GetByIdAsync(documentId);
            if (document is null)
            {
                throw new KeyNotFoundException($"Document with ID '{documentId}' was not found.");
            }

            _logger.LogInformation("Parsing and chunking document {DocumentId} ({FileType})", documentId, document.FileType);

            // 2. Open document read stream
            await using var fileStream = await _storageService.OpenReadAsync(document.FileUrl, cancellationToken);

            // 3. Initialize OCR engine if enabled
            IOcrEngine? ocrEngine = null;
            if (_ocrOptions.EnableOcr)
            {
                string tessdataPath = _ocrOptions.TessdataPath ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
                if (Directory.Exists(tessdataPath))
                {
                    _logger.LogInformation("Initializing Tesseract OCR with path: {TessdataPath}", tessdataPath);
                    ocrEngine = new TesseractOcrEngine(tessdataPath, _ocrOptions.Languages);
                }
                else
                {
                    _logger.LogWarning("Tessdata directory not found at '{TessdataPath}'. Skipping OCR fallback.", tessdataPath);
                }
            }

            // 4. Parse document pages
            IReadOnlyList<DocumentParser.Models.ParsedPage> pages;
            try
            {
                string fileType = document.FileType?.ToLowerInvariant() ?? string.Empty;
                if (fileType == "pdf")
                {
                    var parser = new PdfParser(ocrEngine, _ocrOptions.Dpi);
                    var parseResult = parser.Parse(fileStream, document.Title);
                    pages = parseResult.Pages;
                }
                else if (fileType == "docx")
                {
                    var parser = new DocxParser();
                    var parseResult = parser.Parse(fileStream, document.Title);
                    pages = parseResult.Pages;
                }
                else
                {
                    throw new NotSupportedException($"File type '{fileType}' is not supported for parsing.");
                }
            }
            finally
            {
                if (ocrEngine is IDisposable disposableOcr)
                {
                    disposableOcr.Dispose();
                }
            }

            int nonEmptyPageCount = pages.Count(page => !page.IsEmpty && !string.IsNullOrWhiteSpace(page.Text));
            int extractedCharacterCount = pages.Sum(page => string.IsNullOrWhiteSpace(page.Text) ? 0 : page.Text.Length);
            _logger.LogInformation(
                "Document {DocumentId} extraction completed. Pages: {PageCount}; non-empty pages: {NonEmptyPageCount}; extracted characters: {ExtractedCharacterCount}.",
                documentId,
                pages.Count,
                nonEmptyPageCount,
                extractedCharacterCount);

            await _notifier.NotifyProgressAsync(documentId, 30, "Đã trích xuất văn bản", cancellationToken);

            // 5. Chunk page-by-page, preserving page number from ChunkDto, using the
            //    admin-configured fixed chunk size (characters).
            ChunkingSettingsDto chunkingSettings = await _chunkingSettingsService.GetSettingsAsync(cancellationToken);
            var chunkDtos = _chunkingService.ChunkDocument(pages, chunkingSettings.ChunkSizeCharacters);
            int skippedEmptyChunks = chunkDtos.Count(dto => string.IsNullOrWhiteSpace(dto.Content));
            var validChunkDtos = chunkDtos
                .Where(dto => !string.IsNullOrWhiteSpace(dto.Content))
                .ToList();

            _logger.LogInformation(
                "Document {DocumentId} chunked into {ChunkCount} chunks; skipped {SkippedChunkCount} empty chunks.",
                documentId,
                chunkDtos.Count,
                skippedEmptyChunks);

            // No .Trim() here: fixed-size chunking hard-cuts on a character budget with no
            // regard for word/whitespace boundaries, so trimming would silently shrink a
            // chunk below the admin-configured size whenever a cut lands on whitespace.
            var newChunks = validChunkDtos.Select(dto => new Chunk
            {
                ChunkId = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = dto.ChunkIndex,
                Content = dto.Content,
                CreatedAt = UnspecifiedNow()
            }).ToList();

            if (newChunks.Count == 0)
            {
                throw new InvalidOperationException(
                    "No extractable text was found in the document, so no chunks could be created.");
            }

            await _notifier.NotifyProgressAsync(documentId, 50, "Đã chia chunk", cancellationToken);

            // 6. Embed chunks
            IReadOnlyList<string> texts = newChunks.Select(c => c.Content).ToList();
            IReadOnlyList<float[]> embeddings = await _embeddingService.CreateEmbeddingsAsync(texts, cancellationToken);

            int firstEmbeddingDimension = embeddings.FirstOrDefault(e => e is { Length: > 0 })?.Length ?? 0;
            _logger.LogInformation(
                "Embedding service returned {EmbeddingCount} embeddings for {ChunkCount} chunks. First valid embedding dimension: {EmbeddingDimension}.",
                embeddings.Count,
                newChunks.Count,
                firstEmbeddingDimension);

            // 7. Attach each chunk's embedding so it saves together with the chunk row.
            int chunksWithEmbeddings = 0;
            int skippedInvalidVectors = 0;

            for (int i = 0; i < newChunks.Count; i++)
            {
                float[]? embedding = i < embeddings.Count ? embeddings[i] : null;
                if (embedding is null || embedding.Length == 0)
                {
                    skippedInvalidVectors++;
                    _logger.LogWarning(
                        "Skipping embedding for chunk {ChunkIndex} of document {DocumentId} because it was null or empty.",
                        newChunks[i].ChunkIndex,
                        documentId);
                    continue;
                }

                newChunks[i].Embedding = new Vector(embedding);
                chunksWithEmbeddings++;
            }

            _logger.LogInformation(
                "Prepared {EmbeddedCount} chunk embeddings for document {DocumentId}; skipped {SkippedVectorCount} invalid vectors.",
                chunksWithEmbeddings,
                documentId,
                skippedInvalidVectors);

            if (chunksWithEmbeddings == 0)
            {
                throw new InvalidOperationException(
                    "No valid embedding vectors were generated, so document processing cannot continue.");
            }

            await _notifier.NotifyProgressAsync(documentId, 80, "Đã tạo embedding", cancellationToken);

            // 8. Bulk delete old chunks and insert new ones
            var existingChunks = _context.Chunks.Where(c => c.DocumentId == documentId);
            _context.Chunks.RemoveRange(existingChunks);
            await _context.Chunks.AddRangeAsync(newChunks, cancellationToken);

            await _unitOfWork.Documents.UpdateStatusAsync(documentId, "completed");

            if (job is not null)
            {
                job.JobStatus = "completed";
                job.FinishedAt = UnspecifiedNow();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Document {DocumentId} processed into {ChunkCount} chunks with embeddings", documentId, newChunks.Count);

            await _notifier.NotifyCompletedAsync(documentId, newChunks.Count, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for document {DocumentId}", documentId);

            if (job is not null)
            {
                job.JobStatus = "failed";
                job.FinishedAt = UnspecifiedNow();
                job.ErrorMessage = ex.Message;
            }

            try
            {
                await _unitOfWork.Documents.UpdateStatusAsync(documentId, "failed");
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to save error status to database for document {DocumentId}", documentId);
            }

            await _notifier.NotifyFailedAsync(documentId, ex.Message, CancellationToken.None);
        }
    }

    // The processing_jobs timestamp columns are "timestamp without time zone".
    // Npgsql rejects a UTC-kind DateTime for those, so store an Unspecified-kind value.
    private static DateTime UnspecifiedNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
