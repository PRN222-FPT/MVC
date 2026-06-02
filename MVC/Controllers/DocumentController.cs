using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace MVC.Controllers;

/// <summary>
/// Document library and upload workflow for the RAG ingestion pipeline.
/// </summary>
[Authorize(Roles = UserRoles.Teacher)]
[Route("Documents")]
public class DocumentController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly UploadOptions _uploadOptions;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IDocumentService documentService,
        IOptions<UploadOptions> uploadOptions,
        ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _uploadOptions = uploadOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Library));
    }

    [HttpGet("Library")]
    public async Task<IActionResult> Library(CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentListItemDto> documents = await _documentService.GetDocumentsAsync(cancellationToken);

        var viewModel = new DocumentLibraryViewModel
        {
            Documents = documents.Select(MapDocumentListItem).ToList()
        };

        return View(viewModel);
    }

    [HttpGet("Statuses")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Statuses(CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentListItemDto> documents = await _documentService.GetDocumentsAsync(cancellationToken);

        return Json(documents.Select(document =>
        {
            DocumentListItemViewModel viewModel = MapDocumentListItem(document);
            return new
            {
                documentId = viewModel.DocumentId,
                status = viewModel.Status,
                statusBadgeClass = viewModel.StatusBadgeClass,
                isTerminal = IsTerminalStatus(viewModel.Status)
            };
        }));
    }

    [HttpGet("Upload")]
    public IActionResult Upload()
    {
        return View(BuildUploadPageViewModel(new UploadDocumentForm()));
    }

    // Backward-compatible route for older sidebar links.
    [HttpGet("UploadPage")]
    public IActionResult UploadPage()
    {
        return RedirectToAction(nameof(Upload));
    }

    /// <summary>
    /// Uploads, parses, chunks, and persists a PDF or DOCX file.
    /// </summary>
    /// <remarks>
    /// Validates type and size, stores the file and database row, then invokes
    /// the ingestion pipeline before returning to the library page.
    /// </remarks>
    [HttpPost("Upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(25L * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [Bind(Prefix = "Form")] UploadDocumentForm form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(BuildUploadPageViewModel(form));
        }

        IFormFile file = form.File;
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!_uploadOptions.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                "Form.File",
                $"Unsupported file type '{extension}'. Allowed: {string.Join(", ", _uploadOptions.AllowedExtensions)}.");
            return View(BuildUploadPageViewModel(form));
        }

        if (file.Length > _uploadOptions.MaxFileSizeBytes)
        {
            long limitMb = _uploadOptions.MaxFileSizeBytes / (1024 * 1024);
            ModelState.AddModelError("Form.File", $"File exceeds the {limitMb} MB limit.");
            return View(BuildUploadPageViewModel(form));
        }

        Guid? uploadedBy = TryGetCurrentUserId();
        await using Stream content = file.OpenReadStream();

        var request = new DocumentUploadRequest
        {
            Content = content,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length,
            ChapterId = form.ChapterId,
            Title = form.Title,
            UploadedBy = uploadedBy
        };

        try
        {
            UploadDocumentResult result = await _documentService.InitiateUploadAsync(request, cancellationToken);

            _logger.LogInformation(
                "Accepted upload '{FileName}' ({Bytes} bytes) -> document {DocumentId}",
                file.FileName, file.Length, result.DocumentId);

            TempData["Success"] = $"'{result.Title}' was uploaded and queued for processing.";

            return RedirectToAction(nameof(Library));
        }
        catch (KeyNotFoundException ex)
        {
            ModelState.AddModelError("Form.ChapterId", ex.Message);
            return View(BuildUploadPageViewModel(form));
        }
    }

    private DocumentUploadPageViewModel BuildUploadPageViewModel(UploadDocumentForm form) =>
        new()
        {
            Form = form,
            MaxFileSizeBytes = _uploadOptions.MaxFileSizeBytes,
            AllowedExtensionsText = string.Join(", ", _uploadOptions.AllowedExtensions.Select(e => e.TrimStart('.').ToUpperInvariant()))
        };

    private static DocumentListItemViewModel MapDocumentListItem(DocumentListItemDto document) =>
        new()
        {
            DocumentId = document.DocumentId,
            Title = document.Title,
            FileType = document.FileType,
            Status = document.Status,
            CreatedAt = document.CreatedAt,
            FileUrl = document.FileUrl
        };

    private static bool IsTerminalStatus(string status) =>
        status.Equals("completed", StringComparison.OrdinalIgnoreCase)
        || status.Equals("processed", StringComparison.OrdinalIgnoreCase)
        || status.Equals("failed", StringComparison.OrdinalIgnoreCase);

    private Guid? TryGetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out Guid parsed) ? parsed : null;
    }
}
