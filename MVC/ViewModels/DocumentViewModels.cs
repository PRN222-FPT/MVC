using ServiceLayer.DTOs;

namespace MVC.ViewModels;

public sealed class DocumentUploadPageViewModel
{
    public UploadDocumentForm Form { get; set; } = new();

    public IEnumerable<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> SubjectOptions { get; set; } = [];

    public long MaxFileSizeBytes { get; set; }

    public string AllowedExtensionsText { get; set; } = string.Empty;

    public long MaxFileSizeMb => MaxFileSizeBytes / (1024 * 1024);
}

public sealed class DocumentLibraryViewModel
{
    public string SearchTerm { get; set; } = string.Empty;

    public IReadOnlyList<DocumentListItemViewModel> Documents { get; set; } = [];

    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchTerm);
}

public sealed class DocumentListItemViewModel
{
    public Guid DocumentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string SubjectCode { get; set; } = string.Empty;

    public string SubjectName { get; set; } = string.Empty;

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

public sealed class DocumentChunksViewModel
{
    public Guid DocumentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<ChunkItemViewModel> Chunks { get; set; } = [];

    public bool IsCompleted => Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
        || Status.Equals("processed", StringComparison.OrdinalIgnoreCase);

    public string StatusBadgeClass => Status.ToLowerInvariant() switch
    {
        "completed" or "processed" => "bg-success-subtle text-success",
        "failed" => "bg-danger-subtle text-danger",
        "processing" => "bg-info-subtle text-info",
        "queued" => "bg-warning-subtle text-warning",
        _ => "bg-primary-subtle text-primary"
    };
}

public sealed class ChunkItemViewModel
{
    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
}

public sealed class DocumentViewerViewModel
{
    public Guid DocumentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string InlineUrl { get; set; } = string.Empty;

    public string DownloadUrl { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public bool CanPreviewInline => FileType.Equals("pdf", StringComparison.OrdinalIgnoreCase);

    public bool IsTerminal => Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
        || Status.Equals("processed", StringComparison.OrdinalIgnoreCase)
        || Status.Equals("failed", StringComparison.OrdinalIgnoreCase);

    public bool IsCompleted => Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
        || Status.Equals("processed", StringComparison.OrdinalIgnoreCase);

    public string StatusBadgeClass => Status.ToLowerInvariant() switch
    {
        "completed" or "processed" => "bg-success-subtle text-success",
        "failed" => "bg-danger-subtle text-danger",
        "processing" => "bg-info-subtle text-info",
        "queued" => "bg-warning-subtle text-warning",
        _ => "bg-primary-subtle text-primary"
    };
}
