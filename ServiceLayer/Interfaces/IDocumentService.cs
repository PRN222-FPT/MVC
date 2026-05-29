using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Orchestrates document ingestion. The controller delegates here after basic HTTP validation.
/// </summary>
public interface IDocumentService
{
    Task<IReadOnlyList<DocumentListItemDto>> GetDocumentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the uploaded file, parses it, chunks extracted text, saves chunks, and returns
    /// the final document status.
    /// </summary>
    Task<UploadDocumentResult> InitiateUploadAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default);
}
