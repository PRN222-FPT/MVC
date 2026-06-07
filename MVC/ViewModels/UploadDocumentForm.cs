using System.ComponentModel.DataAnnotations;

namespace MVC.ViewModels;

/// <summary>
/// Multipart form bound by <c>DocumentController.Upload</c>. Keeps the HTTP-specific
/// <see cref="IFormFile"/> at the MVC boundary; the service layer receives a plain stream.
/// </summary>
public class UploadDocumentForm
{
    /// <summary>PDF or DOCX file to ingest (max 20 MB).</summary>
    [Required(ErrorMessage = "A file is required.")]
    public IFormFile File { get; set; } = null!;

    /// <summary>Optional target chapter id. When omitted, a default "Uploads" chapter is used.</summary>
    public Guid? ChapterId { get; set; }

    [Required(ErrorMessage = "A subject is required.")]
    public Guid? SubjectId { get; set; }

    /// <summary>Optional display title. Defaults to the uploaded file name.</summary>
    [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters.")]
    public string? Title { get; set; }
}
