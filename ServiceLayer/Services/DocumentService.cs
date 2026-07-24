using DataAccessLayer.Models;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Orchestrates an upload: saves the file, inserts the document row, creates a
/// ProcessingJob, then enqueues the document id for background processing.
/// The background worker picks it up and runs parse → chunk → embed → upsert → update status.
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private const string DefaultChapterTitle = "Uploads";
    private const int MaxSearchTermLength = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly Prn222Context _context;
    private readonly IStorageService _storage;
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IUnitOfWork unitOfWork,
        Prn222Context context,
        IStorageService storage,
        IBackgroundTaskQueue queue,
        ILogger<DocumentService> logger)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _storage = storage;
        _queue = queue;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentListItemDto>> GetDocumentsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Document> query = _unitOfWork.Documents.Query()
            .AsNoTracking();

        string? normalizedSearchTerm = NormalizeSearchTerm(searchTerm);
        if (normalizedSearchTerm is not null)
        {
            string loweredSearchTerm = normalizedSearchTerm.ToLower();
            bool searchesDocumentId = Guid.TryParse(normalizedSearchTerm, out Guid documentId);

            query = query.Where(document =>
                document.Title.ToLower().Contains(loweredSearchTerm)
                || (document.FileType != null && document.FileType.ToLower().Contains(loweredSearchTerm))
                || (document.Status != null && document.Status.ToLower().Contains(loweredSearchTerm))
                || (searchesDocumentId && document.DocumentId == documentId));
        }

        return await query
            .OrderByDescending(document => document.CreatedAt)
            .ThenBy(document => document.Title)
            .Select(document => new DocumentListItemDto(
                document.DocumentId,
                document.Title,
                document.FileType ?? "unknown",
                document.Status ?? "pending",
                document.CreatedAt,
                document.FileUrl,
                document.SubjectId,
                document.Subject.SubjectCode,
                document.Subject.SubjectName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TeacherUploadSubjectDto>> GetUploadableSubjectsAsync(
        Guid teacherUserId,
        CancellationToken cancellationToken = default)
    {
        Teacher teacher = await ResolveTeacherForUserAsync(teacherUserId, cancellationToken);

        return await _context.TeacherSubjects
            .AsNoTracking()
            .Where(assignment => assignment.TeacherId == teacher.TeacherId && assignment.IsHeadOfDepartment)
            .OrderBy(assignment => assignment.Subject.SubjectCode)
            .ThenBy(assignment => assignment.Subject.SubjectName)
            .Select(assignment => new TeacherUploadSubjectDto(
                assignment.SubjectId,
                assignment.Subject.SubjectCode,
                assignment.Subject.SubjectName))
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentFileDto> OpenDocumentFileAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        Document? document = await _unitOfWork.Documents
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.DocumentId == documentId, cancellationToken);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document '{documentId}' does not exist.");
        }

        if (string.IsNullOrWhiteSpace(document.FileUrl) || !_storage.Exists(document.FileUrl))
        {
            throw new FileNotFoundException("Stored document file was not found.", document.FileUrl);
        }

        Stream content = await _storage.OpenReadAsync(document.FileUrl, cancellationToken);
        string fileName = Path.GetFileName(document.FileUrl.Replace('/', Path.DirectorySeparatorChar));
        string fileType = document.FileType ?? NormalizeFileType(Path.GetExtension(fileName));

        return new DocumentFileDto(
            document.DocumentId,
            document.Title,
            fileName,
            fileType,
            GetContentType(fileType),
            content,
            document.Status ?? "pending");
    }

    public async Task<DocumentChunksResultDto> GetDocumentChunksAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        Document? document = await _unitOfWork.Documents
            .Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.DocumentId == documentId, cancellationToken);
        if (document is null)
        {
            throw new KeyNotFoundException($"Document '{documentId}' does not exist.");
        }

        IReadOnlyList<Chunk> chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(documentId);

        return new DocumentChunksResultDto(
            document.DocumentId,
            document.Title,
            document.Status ?? "pending",
            chunks
                .OrderBy(chunk => chunk.ChunkIndex)
                .Select(chunk => new DocumentChunkDto(chunk.ChunkId, chunk.ChunkIndex, chunk.Content, chunk.CreatedAt))
                .ToList());
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
        if (request.SubjectId == Guid.Empty)
            throw new ArgumentException("Subject is required.", nameof(request));
        if (!request.UploadedBy.HasValue || request.UploadedBy.Value == Guid.Empty)
            throw new UnauthorizedAccessException("Authenticated teacher user is required to upload documents.");

        Teacher teacher = await ResolveTeacherForUserAsync(request.UploadedBy.Value, cancellationToken);
        await EnsureTeacherCanUploadForSubjectAsync(teacher.TeacherId, request.SubjectId, cancellationToken);

        Guid chapterId = await ResolveChapterIdAsync(request.ChapterId, request.SubjectId, cancellationToken);

        Guid documentId = Guid.NewGuid();
        string extension = Path.GetExtension(request.FileName);

        // 1) Save the file via the storage abstraction.
        string relativePath = await _storage.SaveAsync(
            documentId, request.Content, request.FileName, cancellationToken);

        // 2) Insert the documents row. The processor updates the status to processed/failed.
        string title = string.IsNullOrWhiteSpace(request.Title)
            ? Path.GetFileNameWithoutExtension(request.FileName)
            : request.Title.Trim();
        if (title.Length > 255)
            title = title[..255];

        var document = new Document
        {
            DocumentId = documentId,
            ChapterId = chapterId,
            SubjectId = request.SubjectId,
            Title = title,
            FileUrl = relativePath,
            FileType = NormalizeFileType(extension),
            Status = "pending",
            UploadedBy = request.UploadedBy,
            UploadedTeacher = teacher.TeacherId
            // CreatedAt left unset — DB default (CURRENT_TIMESTAMP) fills it.
        };
        await _unitOfWork.Documents.CreateAsync(document);

        // 3) Create a ProcessingJob record for auditability, then run ingestion immediately.
        var job = new ProcessingJob
        {
            JobId = Guid.NewGuid(),
            DocumentId = documentId,
            JobStatus = "queued"
            // StartedAt/FinishedAt set by the processor.
        };
        await _context.ProcessingJobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4) Hand off to the background worker via the in-process queue.
        //    The worker calls IDocumentProcessor.ProcessAsync (parse → chunk → embed → save to pgvector).
        await _queue.EnqueueAsync(documentId, cancellationToken);

        _logger.LogInformation(
            "Document {DocumentId} accepted for background processing ({FileType}, {Bytes} bytes, subject {SubjectId}, chapter {ChapterId})",
            documentId, document.FileType, request.Length, request.SubjectId, chapterId);

        return new UploadDocumentResult(
            documentId,
            title,
            "queued",
            document.FileType ?? "unknown",
            relativePath,
            ChunkCount: 0);
    }

    // -------------------------------------------------------------------------
    // Chapter resolution
    // -------------------------------------------------------------------------

    private async Task<Guid> ResolveChapterIdAsync(
        Guid? requested,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        if (requested.HasValue && requested.Value != Guid.Empty)
        {
            Chapter? chapter = await _context.Chapters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ChapterId == requested.Value, cancellationToken);
            if (chapter is null)
                throw new KeyNotFoundException($"Chapter '{requested.Value}' does not exist.");
            if (chapter.SubjectId != subjectId)
                throw new InvalidOperationException("Selected chapter does not belong to the selected subject.");
            return requested.Value;
        }

        return await EnsureDefaultChapterAsync(subjectId, cancellationToken);
    }

    /// <summary>
    /// Dev convenience: get-or-create a default Subject + "Uploads" Chapter so uploads
    /// work without the caller knowing a chapter id (the Document FK requires a chapter).
    /// </summary>
    private async Task<Guid> EnsureDefaultChapterAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        Chapter? existing = await _context.Chapters
            .FirstOrDefaultAsync(c => c.SubjectId == subjectId && c.ChapterTitle == DefaultChapterTitle, cancellationToken);
        if (existing is not null)
            return existing.ChapterId;

        Subject? subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId, cancellationToken);
        if (subject is null)
        {
            throw new KeyNotFoundException($"Subject '{subjectId}' does not exist.");
        }

        var chapter = new Chapter
        {
            ChapterId = Guid.NewGuid(),
            SubjectId = subject.SubjectId,
            ChapterTitle = DefaultChapterTitle,
            ChapterOrder = 1
        };
        await _context.Chapters.AddAsync(chapter, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created default chapter '{Title}' ({ChapterId}) for uploads",
            DefaultChapterTitle, chapter.ChapterId);

        return chapter.ChapterId;
    }

    private async Task<Teacher> ResolveTeacherForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        User? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (user is null || !string.Equals(user.Role, UserRoles.Teacher, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only teacher accounts can upload documents.");
        }

        Teacher? teacher = await _context.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Email != null && candidate.Email.ToLower() == user.Email.ToLower(),
                cancellationToken);
        if (teacher is null)
        {
            throw new UnauthorizedAccessException("Teacher profile was not found for the authenticated user.");
        }

        return teacher;
    }

    private async Task EnsureTeacherCanUploadForSubjectAsync(
        Guid teacherId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        bool canUpload = await _context.TeacherSubjects
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.TeacherId == teacherId
                    && assignment.SubjectId == subjectId
                    && assignment.IsHeadOfDepartment,
                cancellationToken);
        if (!canUpload)
        {
            throw new UnauthorizedAccessException(
                "Only the head teacher assigned to this subject can upload documents for it.");
        }
    }

    private static string NormalizeFileType(string extension) =>
        string.IsNullOrEmpty(extension)
            ? "unknown"
            : extension.TrimStart('.').ToLowerInvariant();

    private static string GetContentType(string fileType) =>
        fileType.TrimStart('.').ToLowerInvariant() switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };

    private static string? NormalizeSearchTerm(string? searchTerm)
    {
        string? normalized = searchTerm?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length > MaxSearchTermLength
            ? normalized[..MaxSearchTermLength]
            : normalized;
    }
}
