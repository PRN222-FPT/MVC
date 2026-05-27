using DataAccessLayer;
using DataAccessLayer.Models;
using DataAccessLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

/// <summary>
/// Default <see cref="IDocumentService"/>. Saves the uploaded file to disk, inserts a
/// "documents" row with status "pending", and returns the new document id.
///
/// NOTE (Task 2): file saving is inline here. Task 3 extracts it behind IStorageService
/// and adds background-job enqueue. Status starts as "pending"; a worker advances it later.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private const string DefaultChapterTitle = "Uploads";
    private const string DefaultSubjectCode = "GENERAL";

    private readonly IDocumentRepository _documentRepository;
    private readonly Prn222Context _context;
    private readonly UploadOptions _options;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documentRepository,
        Prn222Context context,
        IOptions<UploadOptions> options,
        ILogger<DocumentService> logger)
    {
        _documentRepository = documentRepository;
        _context = context;
        _options = options.Value;
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

        // Use the document id as the storage folder so files never collide.
        Guid documentId = Guid.NewGuid();
        string extension = Path.GetExtension(request.FileName);
        string relativePath = await SaveFileAsync(documentId, request, cancellationToken);

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
            // Avoids Npgsql 'timestamp without time zone' vs UTC DateTime conflict.
        };

        await _documentRepository.CreateAsync(document);

        _logger.LogInformation(
            "Document {DocumentId} stored with status pending ({FileType}, {Bytes} bytes, chapter {ChapterId})",
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
    /// work without the caller knowing a chapter id. The Document FK requires a chapter.
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

    // -------------------------------------------------------------------------
    // File persistence (Task 3 will extract this into IStorageService)
    // -------------------------------------------------------------------------

    private async Task<string> SaveFileAsync(
        Guid documentId, DocumentUploadRequest request, CancellationToken cancellationToken)
    {
        // Strip any directory components from the client file name (path-traversal guard).
        string safeFileName = Path.GetFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = documentId.ToString();

        string root = Path.IsPathRooted(_options.StorageRoot)
            ? _options.StorageRoot
            : Path.Combine(Directory.GetCurrentDirectory(), _options.StorageRoot);

        string folder = Path.Combine(root, documentId.ToString());
        Directory.CreateDirectory(folder);

        string fullPath = Path.Combine(folder, safeFileName);
        await using (var fileStream = new FileStream(
            fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (request.Content.CanSeek)
                request.Content.Position = 0;
            await request.Content.CopyToAsync(fileStream, cancellationToken);
        }

        // Store a forward-slash relative path (stable across OSes) in FileUrl.
        return $"{_options.StorageRoot}/{documentId}/{safeFileName}".Replace('\\', '/');
    }

    private static string NormalizeFileType(string extension) =>
        string.IsNullOrEmpty(extension)
            ? "unknown"
            : extension.TrimStart('.').ToLowerInvariant();
}
