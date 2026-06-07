using System.Security.Claims;
using System.Net.Mime;
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
[Authorize(Roles = $"{UserRoles.Student},{UserRoles.Teacher}")]
[Route("Documents")]
public class DocumentController : Controller
{
    private const int MaxSearchTermLength = 100;

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
    [Authorize(Roles = UserRoles.Teacher)]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Library));
    }

    [HttpGet("Library")]
    [Authorize(Roles = UserRoles.Teacher)]
    public async Task<IActionResult> Library([FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        string? normalizedSearchTerm = NormalizeSearchTerm(searchTerm);
        IReadOnlyList<DocumentListItemDto> documents = await _documentService.GetDocumentsAsync(
            normalizedSearchTerm,
            cancellationToken);

        var viewModel = new DocumentLibraryViewModel
        {
            SearchTerm = normalizedSearchTerm ?? string.Empty,
            Documents = documents.Select(MapDocumentListItem).ToList()
        };

        return View(viewModel);
    }

    [HttpGet("Statuses")]
    [Authorize(Roles = UserRoles.Teacher)]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Statuses(CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentListItemDto> documents = await _documentService.GetDocumentsAsync(
            cancellationToken: cancellationToken);

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
    [Authorize(Roles = UserRoles.Teacher)]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        return View(await BuildUploadPageViewModelAsync(new UploadDocumentForm(), cancellationToken));
    }

    // Backward-compatible route for older sidebar links.
    [HttpGet("UploadPage")]
    [Authorize(Roles = UserRoles.Teacher)]
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
    [Authorize(Roles = UserRoles.Teacher)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(25L * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [Bind(Prefix = "Form")] UploadDocumentForm form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildUploadPageViewModelAsync(form, cancellationToken));
        }

        IFormFile file = form.File;
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!_uploadOptions.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                "Form.File",
                $"Unsupported file type '{extension}'. Allowed: {string.Join(", ", _uploadOptions.AllowedExtensions)}.");
            return View(await BuildUploadPageViewModelAsync(form, cancellationToken));
        }

        if (file.Length > _uploadOptions.MaxFileSizeBytes)
        {
            long limitMb = _uploadOptions.MaxFileSizeBytes / (1024 * 1024);
            ModelState.AddModelError("Form.File", $"File exceeds the {limitMb} MB limit.");
            return View(await BuildUploadPageViewModelAsync(form, cancellationToken));
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
            SubjectId = form.SubjectId ?? Guid.Empty,
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

            return RedirectToAction(nameof(ViewDocument), new { documentId = result.DocumentId });
        }
        catch (KeyNotFoundException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildUploadPageViewModelAsync(form, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildUploadPageViewModelAsync(form, cancellationToken));
        }
        catch (UnauthorizedAccessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(await BuildUploadPageViewModelAsync(form, cancellationToken));
        }
    }

    [HttpGet("{documentId:guid}/View")]
    public async Task<IActionResult> ViewDocument(Guid documentId, CancellationToken cancellationToken)
    {
        DocumentFileDto document = await _documentService.OpenDocumentFileAsync(documentId, cancellationToken);

        return View("View", new DocumentViewerViewModel
        {
            DocumentId = document.DocumentId,
            Title = document.Title,
            FileName = document.FileName,
            FileType = document.FileType,
            ContentType = document.ContentType,
            InlineUrl = Url.Action(nameof(Inline), new { documentId }) ?? string.Empty,
            DownloadUrl = Url.Action(nameof(Download), new { documentId }) ?? string.Empty
        });
    }

    [HttpGet("{documentId:guid}/Inline")]
    public async Task<IActionResult> Inline(Guid documentId, CancellationToken cancellationToken)
    {
        DocumentFileDto document = await _documentService.OpenDocumentFileAsync(documentId, cancellationToken);
        Response.Headers.Append(
            "Content-Disposition",
            new ContentDisposition
            {
                Inline = true,
                FileName = document.FileName
            }.ToString());

        return File(document.Content, document.ContentType);
    }

    [HttpGet("{documentId:guid}/Download")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken cancellationToken)
    {
        DocumentFileDto document = await _documentService.OpenDocumentFileAsync(documentId, cancellationToken);
        return File(document.Content, document.ContentType, document.FileName);
    }

    private async Task<DocumentUploadPageViewModel> BuildUploadPageViewModelAsync(
        UploadDocumentForm form,
        CancellationToken cancellationToken)
    {
        Guid? currentUserId = TryGetCurrentUserId();
        IReadOnlyList<TeacherUploadSubjectDto> uploadableSubjects = [];
        if (currentUserId.HasValue)
        {
            try
            {
                uploadableSubjects = await _documentService.GetUploadableSubjectsAsync(currentUserId.Value, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                uploadableSubjects = [];
            }
        }

        return new DocumentUploadPageViewModel
        {
            Form = form,
            SubjectOptions = uploadableSubjects.Select(subject => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                $"{subject.SubjectCode} - {subject.SubjectName}",
                subject.SubjectId.ToString())),
            MaxFileSizeBytes = _uploadOptions.MaxFileSizeBytes,
            AllowedExtensionsText = string.Join(", ", _uploadOptions.AllowedExtensions.Select(e => e.TrimStart('.').ToUpperInvariant()))
        };
    }

    private static DocumentListItemViewModel MapDocumentListItem(DocumentListItemDto document) =>
        new()
        {
            DocumentId = document.DocumentId,
            Title = document.Title,
            FileType = document.FileType,
            Status = document.Status,
            SubjectCode = document.SubjectCode,
            SubjectName = document.SubjectName,
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
