using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace MVC.Controllers;

/// <summary>
/// API endpoints for uploading documents into the RAG ingestion pipeline.
/// </summary>
[ApiController]
[Route("Documents")]
[Produces("application/json")]
public class DocumentController : ControllerBase
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

    /// <summary>
    /// Uploads a PDF or DOCX file for asynchronous processing.
    /// </summary>
    /// <remarks>
    /// Validates type (PDF/DOCX) and size (max 20 MB), stores the file and a
    /// "pending" database row, then returns 202 Accepted immediately. The actual
    /// parsing/processing is performed later by a background worker.
    /// </remarks>
    /// <response code="202">Upload accepted; processing will continue asynchronously.</response>
    /// <response code="400">The request is missing a file, or the file type/size is invalid.</response>
    [HttpPost("Upload")]
    [RequestSizeLimit(25L * 1024 * 1024)] // hard cap slightly above the 20 MB business limit
    [ProducesResponseType(typeof(UploadDocumentResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        [FromForm] UploadDocumentForm form,
        CancellationToken cancellationToken)
    {
        IFormFile? file = form.File;

        if (file is null || file.Length == 0)
            return BadRequest(Problem400("A non-empty file is required."));

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_uploadOptions.AllowedExtensions.Contains(extension))
            return BadRequest(Problem400(
                $"Unsupported file type '{extension}'. Allowed: {string.Join(", ", _uploadOptions.AllowedExtensions)}."));

        if (file.Length > _uploadOptions.MaxFileSizeBytes)
        {
            long limitMb = _uploadOptions.MaxFileSizeBytes / (1024 * 1024);
            return BadRequest(Problem400($"File exceeds the {limitMb} MB limit."));
        }

        await using Stream content = file.OpenReadStream();

        var request = new DocumentUploadRequest
        {
            Content = content,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Length = file.Length,
            ChapterId = form.ChapterId,
            Title = form.Title
        };

        UploadDocumentResult result = await _documentService.InitiateUploadAsync(request, cancellationToken);

        _logger.LogInformation(
            "Accepted upload '{FileName}' ({Bytes} bytes) -> document {DocumentId}",
            file.FileName, file.Length, result.DocumentId);

        // 202 Accepted: stored as pending; processing continues asynchronously.
        return AcceptedAtAction(
            actionName: nameof(Upload),
            routeValues: new { id = result.DocumentId },
            value: result);
    }

    private static ValidationProblemDetails Problem400(string detail) =>
        new() { Title = "Invalid upload request.", Detail = detail, Status = StatusCodes.Status400BadRequest };
}
