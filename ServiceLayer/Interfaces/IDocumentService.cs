using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Orchestrates document ingestion. The controller delegates here after basic HTTP validation.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Persists the uploaded file (disk + database) with status "pending" and returns the
    /// new document id. Heavy processing is handled asynchronously by a background worker.
    /// </summary>
    Task<UploadDocumentResult> InitiateUploadAsync(
        DocumentUploadRequest request,
        CancellationToken cancellationToken = default);
}
