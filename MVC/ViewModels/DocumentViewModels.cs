using ServiceLayer.DTOs;

namespace MVC.ViewModels;

public sealed class DocumentUploadPageViewModel
{
    public UploadDocumentForm Form { get; set; } = new();

    public long MaxFileSizeBytes { get; set; }

    public string AllowedExtensionsText { get; set; } = string.Empty;

    public long MaxFileSizeMb => MaxFileSizeBytes / (1024 * 1024);
}

public sealed class DocumentLibraryViewModel
{
    public IReadOnlyList<DocumentListItemViewModel> Documents { get; set; } = [];
}

public sealed class DocumentListItemViewModel
{
    public Guid DocumentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }

    public string FileUrl { get; set; } = string.Empty;

    public string TypeLabel => FileType.ToLowerInvariant() switch
    {
        "pdf" => "PDF Document",
        "docx" => "Word Document",
        _ => $"{FileType.ToUpperInvariant()} Document"
    };

    public string IconName => FileType.ToLowerInvariant() switch
    {
        "pdf" => "picture_as_pdf",
        "docx" => "description",
        _ => "insert_drive_file"
    };

    public string IconClass => FileType.ToLowerInvariant() switch
    {
        "pdf" => "text-danger",
        "docx" => "text-primary",
        _ => "text-secondary"
    };

    public string StatusBadgeClass => Status.ToLowerInvariant() switch
    {
        "completed" or "processed" => "bg-success-subtle text-success",
        "failed" => "bg-danger-subtle text-danger",
        "processing" => "bg-info-subtle text-info",
        "queued" => "bg-warning-subtle text-warning",
        _ => "bg-primary-subtle text-primary"
    };
}
