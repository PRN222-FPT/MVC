using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Orchestrates document ingestion. The controller delegates here after basic HTTP validation.
/// </summary>
public interface IDocumentService
{
    Task<IReadOnlyList<DocumentListItemDto>> GetDocumentsAsync(
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeacherUploadSubjectDto>> GetUploadableSubjectsAsync(
        Guid teacherUserId,
        CancellationToken cancellationToken = default);

    Task<DocumentFileDto> OpenDocumentFileAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the persisted chunks for a document, ordered by chunk index, along with
    /// the document's current processing status.
    /// </summary>
    Task<DocumentChunksResultDto> GetDocumentChunksAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the uploaded file, parses it, chunks extracted text, saves chunks, and returns
    /// the final document status.
    /// </summary>
    Task<UploadDocumentResult> InitiateUploadAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default);
}
