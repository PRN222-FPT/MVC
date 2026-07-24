using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MVC.Controllers;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;
using Xunit;

namespace Test;

public sealed class DocumentControllerTests
{
    [Fact]
    public void Controller_AllowsStudentsAndTeachersForDocumentViewingRoutes()
    {
        AuthorizeAttribute attribute = Assert.Single(
            typeof(DocumentController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal($"{UserRoles.Student},{UserRoles.Teacher}", attribute.Roles);
    }

    [Theory]
    [InlineData(nameof(DocumentController.Library))]
    [InlineData(nameof(DocumentController.Statuses))]
    [InlineData(nameof(DocumentController.Upload))]
    [InlineData(nameof(DocumentController.UploadPage))]
    public void TeacherManagementActions_RequireTeacherRole(string actionName)
    {
        List<AuthorizeAttribute> attributes = typeof(DocumentController).GetMethods()
            .Where(method => method.Name == actionName)
            .SelectMany(method => method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true))
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.NotEmpty(attributes);
        Assert.All(attributes, attribute => Assert.Equal(UserRoles.Teacher, attribute.Roles));
    }

    [Theory]
    [InlineData(nameof(DocumentController.ViewDocument))]
    [InlineData(nameof(DocumentController.Inline))]
    [InlineData(nameof(DocumentController.Download))]
    public void CitationDocumentActions_DoNotRequireTeacherRole(string actionName)
    {
        bool hasTeacherOnlyAttribute = typeof(DocumentController).GetMethods()
            .Where(method => method.Name == actionName)
            .SelectMany(method => method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true))
            .Cast<AuthorizeAttribute>()
            .Any(attribute => string.Equals(attribute.Roles, UserRoles.Teacher, StringComparison.Ordinal));

        Assert.False(hasTeacherOnlyAttribute);
    }

    [Fact]
    public async Task Library_SearchTerm_PassesTrimmedTermToDocumentService()
    {
        var documentService = new FakeDocumentService
        {
            DocumentsToReturn =
            [
                new DocumentListItemDto(
                    Guid.NewGuid(),
                    "Lecture Notes",
                    "pdf",
                    "processed",
                    new DateTime(2026, 6, 2, 10, 0, 0),
                    "uploads/lecture-notes.pdf",
                    Guid.NewGuid(),
                    "PRN222",
                    "Advanced .NET")
            ]
        };
        var controller = CreateController(documentService);

        IActionResult result = await controller.Library("  lecture  ", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var viewModel = Assert.IsType<DocumentLibraryViewModel>(viewResult.Model);
        Assert.Equal("lecture", documentService.LastSearchTerm);
        Assert.Equal("lecture", viewModel.SearchTerm);
        Assert.Single(viewModel.Documents);
    }

    [Fact]
    public async Task Library_BlankSearchTerm_PassesNullToDocumentService()
    {
        var documentService = new FakeDocumentService();
        var controller = CreateController(documentService);

        IActionResult result = await controller.Library("   ", CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        var viewModel = Assert.IsType<DocumentLibraryViewModel>(viewResult.Model);
        Assert.Null(documentService.LastSearchTerm);
        Assert.Equal(string.Empty, viewModel.SearchTerm);
        Assert.False(viewModel.HasSearch);
    }

    [Fact]
    public async Task ViewDocument_ReturnsViewerModel()
    {
        var documentService = new FakeDocumentService();
        var controller = CreateController(documentService);
        Guid documentId = Guid.NewGuid();

        IActionResult result = await controller.ViewDocument(documentId, CancellationToken.None);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("View", viewResult.ViewName);
        var viewModel = Assert.IsType<DocumentViewerViewModel>(viewResult.Model);
        Assert.Equal(documentId, viewModel.DocumentId);
        Assert.True(viewModel.CanPreviewInline);
    }

    [Fact]
    public async Task Download_ReturnsFileWithDownloadName()
    {
        var documentService = new FakeDocumentService();
        var controller = CreateController(documentService);

        IActionResult result = await controller.Download(Guid.NewGuid(), CancellationToken.None);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("lecture-notes.pdf", fileResult.FileDownloadName);
    }

    private static DocumentController CreateController(FakeDocumentService documentService)
    {
        var uploadOptions = Options.Create(new UploadOptions
        {
            MaxFileSizeBytes = 20 * 1024 * 1024,
            AllowedExtensions = [".pdf", ".docx"]
        });

        return new DocumentController(
            documentService,
            uploadOptions,
            NullLogger<DocumentController>.Instance)
        {
            Url = new FakeUrlHelper()
        };
    }

    private sealed class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext actionContext)
        {
            string documentId = actionContext.Values?.GetType().GetProperty("documentId")?.GetValue(actionContext.Values)?.ToString()
                ?? "document";
            return $"/Documents/{documentId}/{actionContext.Action}";
        }

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => routeName;

        public string? RouteUrl(UrlRouteContext routeContext) => routeContext.RouteName;
    }

    private sealed class FakeDocumentService : IDocumentService
    {
        public string? LastSearchTerm { get; private set; }

        public IReadOnlyList<DocumentListItemDto> DocumentsToReturn { get; init; } = [];

        public Task<IReadOnlyList<DocumentListItemDto>> GetDocumentsAsync(
            string? searchTerm = null,
            CancellationToken cancellationToken = default)
        {
            LastSearchTerm = searchTerm;
            return Task.FromResult(DocumentsToReturn);
        }

        public Task<IReadOnlyList<TeacherUploadSubjectDto>> GetUploadableSubjectsAsync(
            Guid teacherUserId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TeacherUploadSubjectDto> subjects =
            [
                new TeacherUploadSubjectDto(Guid.NewGuid(), "PRN222", "Advanced .NET")
            ];
            return Task.FromResult(subjects);
        }

        public Task<DocumentFileDto> OpenDocumentFileAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentFileDto(
                documentId,
                "Lecture Notes",
                "lecture-notes.pdf",
                "pdf",
                "application/pdf",
                new MemoryStream([1, 2, 3]),
                "completed"));
        }

        public Task<DocumentChunksResultDto> GetDocumentChunksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DocumentChunksResultDto(
                documentId,
                "Lecture Notes",
                "completed",
                []));
        }

        public Task<UploadDocumentResult> InitiateUploadAsync(
            DocumentUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
