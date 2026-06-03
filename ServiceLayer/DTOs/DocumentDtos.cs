namespace ServiceLayer.DTOs;

/// <summary>
/// Service-layer input for an upload. Carries a raw stream + metadata so the
/// ServiceLayer never depends on ASP.NET Core's IFormFile (HTTP boundary stays in MVC).
/// </summary>
public sealed class DocumentUploadRequest
{
    /// <summary>Readable stream positioned at the start of the file content.</summary>
    public required Stream Content { get; init; }

    /// <summary>Original client file name (used for Title default and stored file name).</summary>
    public required string FileName { get; init; }

    /// <summary>Client-reported MIME type, if any.</summary>
    public string? ContentType { get; init; }

    /// <summary>File size in bytes (already validated by the controller).</summary>
    public long Length { get; init; }

    /// <summary>Target chapter. When null, the service uses a default "Uploads" chapter.</summary>
    public Guid? ChapterId { get; init; }

    /// <summary>Target subject. Required for teacher uploads.</summary>
    public Guid SubjectId { get; init; }

    /// <summary>Optional display title. Defaults to the file name without extension.</summary>
    public string? Title { get; init; }

    /// <summary>Optional uploader user id (no auth yet, so usually null).</summary>
    public Guid? UploadedBy { get; init; }
}

/// <summary>
/// Result returned to the client after an upload has been ingested.
/// </summary>
public sealed record UploadDocumentResult(
    Guid DocumentId,
    string Title,
    string Status,
    string FileType,
    string FileUrl,
    int ChunkCount,
    string? ProcessingError = null
);

public sealed record DocumentListItemDto(
    Guid DocumentId,
    string Title,
    string FileType,
    string Status,
    DateTime? CreatedAt,
    string FileUrl,
    Guid SubjectId,
    string SubjectCode,
    string SubjectName
);

public sealed record DocumentFileDto(
    Guid DocumentId,
    string Title,
    string FileName,
    string FileType,
    string ContentType,
    Stream Content);

/// <summary>
/// Represents a structured chunk of document text.
/// </summary>
public sealed record ChunkDto(
    int PageNumber,
    int ChunkIndex,
    string Content
);
