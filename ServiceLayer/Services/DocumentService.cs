using DataAccessLayer.Models;
using DataAccessLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Orchestrates an upload: save the file (IStorageService) -> insert the documents row
/// (status "pending") -> create a queued ProcessingJob -> enqueue the id for the background
/// worker. Returns immediately so the controller can reply 202 Accepted.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private const string DefaultChapterTitle = "Uploads";
    private const string DefaultSubjectCode = "GENERAL";

    private readonly IDocumentRepository _documentRepository;
    private readonly Prn222Context _context;
    private readonly IStorageService _storage;
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documentRepository,
        Prn222Context context,
        IStorageService storage,
        IBackgroundTaskQueue queue,
        ILogger<DocumentService> logger)
    {
        _documentRepository = documentRepository;
        _context = context;
        _storage = storage;
        _queue = queue;
        _logger = logger;
    }

    public async Task<UploadDocumentResult> InitiateUploadAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Content is null)
            throw new ArgumentException("Upload content stream is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("File name is required.", nameof(request));

        Guid chapterId = await ResolveChapterIdAsync(request.ChapterId, cancellationToken);

        Guid documentId = Guid.NewGuid();
        string extension = Path.GetExtension(request.FileName);

        // 1) Save the file via the storage abstraction.
        string relativePath = await _storage.SaveAsync(
            documentId, request.Content, request.FileName, cancellationToken);

        // 2) Insert the documents row (status "pending").
        string title = string.IsNullOrWhiteSpace(request.Title)
            ? Path.GetFileNameWithoutExtension(request.FileName)
            : request.Title.Trim();
        if (title.Length > 255)
            title = title[..255];

        var document = new Document
        {
            DocumentId = documentId,
            ChapterId = chapterId,
            Title = title,
            FileUrl = relativePath,
            FileType = NormalizeFileType(extension),
            Status = "pending",
            UploadedBy = request.UploadedBy
            // CreatedAt left unset — DB default (CURRENT_TIMESTAMP) fills it.
        };
        await _documentRepository.CreateAsync(document);

        // 3) Create a queued ProcessingJob (DB record of the pending work).
        var job = new ProcessingJob
        {
            JobId = Guid.NewGuid(),
            DocumentId = documentId,
            JobStatus = "queued"
            // StartedAt/FinishedAt set by the worker.
        };
        await _context.ProcessingJobs.AddAsync(job, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // 4) Enqueue for the background worker.
        await _queue.EnqueueAsync(documentId, cancellationToken);

        _logger.LogInformation(
            "Document {DocumentId} stored pending and enqueued ({FileType}, {Bytes} bytes, chapter {ChapterId})",
            documentId, document.FileType, request.Length, chapterId);

        return new UploadDocumentResult(
            documentId,
            title,
            document.Status,
            document.FileType ?? "unknown",
            relativePath);
    }

    // -------------------------------------------------------------------------
    // Chapter resolution
    // -------------------------------------------------------------------------

    private async Task<Guid> ResolveChapterIdAsync(Guid? requested, CancellationToken cancellationToken)
    {
        if (requested.HasValue && requested.Value != Guid.Empty)
        {
            bool exists = await _context.Chapters
                .AnyAsync(c => c.ChapterId == requested.Value, cancellationToken);
            if (!exists)
                throw new KeyNotFoundException($"Chapter '{requested.Value}' does not exist.");
            return requested.Value;
        }

        return await EnsureDefaultChapterAsync(cancellationToken);
    }

    /// <summary>
    /// Dev convenience: get-or-create a default Subject + "Uploads" Chapter so uploads
    /// work without the caller knowing a chapter id (the Document FK requires a chapter).
    /// </summary>
    private async Task<Guid> EnsureDefaultChapterAsync(CancellationToken cancellationToken)
    {
        Chapter? existing = await _context.Chapters
            .FirstOrDefaultAsync(c => c.ChapterTitle == DefaultChapterTitle, cancellationToken);
        if (existing is not null)
            return existing.ChapterId;

        Subject? subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectCode == DefaultSubjectCode, cancellationToken);
        if (subject is null)
        {
            subject = new Subject
            {
                SubjectId = Guid.NewGuid(),
                SubjectCode = DefaultSubjectCode,
                SubjectName = "General"
            };
            await _context.Subjects.AddAsync(subject, cancellationToken);
        }

        var chapter = new Chapter
        {
            ChapterId = Guid.NewGuid(),
            SubjectId = subject.SubjectId,
            ChapterTitle = DefaultChapterTitle,
            ChapterOrder = 1
        };
        await _context.Chapters.AddAsync(chapter, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created default chapter '{Title}' ({ChapterId}) for uploads",
            DefaultChapterTitle, chapter.ChapterId);

        return chapter.ChapterId;
    }

    private static string NormalizeFileType(string extension) =>
        string.IsNullOrEmpty(extension)
            ? "unknown"
            : extension.TrimStart('.').ToLowerInvariant();
}
