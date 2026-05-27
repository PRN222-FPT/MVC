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

    /// <summary>Optional display title. Defaults to the file name without extension.</summary>
    public string? Title { get; init; }

    /// <summary>Optional uploader user id (no auth yet, so usually null).</summary>
    public Guid? UploadedBy { get; init; }
}

/// <summary>
/// Result returned to the client after an upload is accepted for async processing.
/// </summary>
public sealed record UploadDocumentResult(
    Guid DocumentId,
    string Title,
    string Status,
    string FileType,
    string FileUrl
);
