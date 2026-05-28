using DataAccessLayer.Models;
using DataAccessLayer.Repositories;
using DocumentParser.Ocr;
using DocumentParser.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

/// <summary>
/// Background processing for a queued document:
/// Loads document metadata, opens the file stream, parses via PdfParser/DocxParser,
/// chunks the text recursively page-by-page, and commits chunks to the database.
/// </summary>
public sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly Prn222Context _context;
    private readonly IDocumentRepository _documentRepository;
    private readonly IStorageService _storageService;
    private readonly IRecursiveChunkingService _chunkingService;
    private readonly OcrOptions _ocrOptions;
    private readonly ILogger<DocumentProcessor> _logger;

    public DocumentProcessor(
        Prn222Context context,
        IDocumentRepository documentRepository,
        IStorageService storageService,
        IRecursiveChunkingService chunkingService,
        IOptions<OcrOptions> ocrOptions,
        ILogger<DocumentProcessor> logger)
    {
        _context = context;
        _documentRepository = documentRepository;
        _storageService = storageService;
        _chunkingService = chunkingService;
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
                await _context.SaveChangesAsync(cancellationToken);
            }

            // 1. Fetch document metadata
            var document = await _documentRepository.GetByIdAsync(documentId);
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

            // 5. Chunk page-by-page (Option 2) returning ChunkDto
            var chunkDtos = _chunkingService.ChunkDocument(pages);
            var newChunks = chunkDtos.Select(dto => new Chunk
            {
                ChunkId = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = dto.ChunkIndex,
                Content = dto.Content,
                CreatedAt = UnspecifiedNow()
            }).ToList();

            // 6. Bulk Delete old chunks and Insert new chunks atomically
            var existingChunks = _context.Chunks.Where(c => c.DocumentId == documentId);
            _context.Chunks.RemoveRange(existingChunks);

            await _context.Chunks.AddRangeAsync(newChunks, cancellationToken);

            await _documentRepository.UpdateStatusAsync(documentId, "processed");

            if (job is not null)
            {
                job.JobStatus = "done";
                job.FinishedAt = UnspecifiedNow();
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Document {DocumentId} successfully processed into {ChunkCount} chunks", documentId, newChunks.Count);
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
                await _documentRepository.UpdateStatusAsync(documentId, "failed");
                await _context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to save error status to database for document {DocumentId}", documentId);
            }
        }
    }

    // The processing_jobs timestamp columns are "timestamp without time zone".
    // Npgsql rejects a UTC-kind DateTime for those, so store an Unspecified-kind value.
    private static DateTime UnspecifiedNow() =>
        DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
