namespace ServiceLayer.Interfaces;

/// <summary>
/// Processes a single queued document. Invoked by the background worker, once per dequeued id.
/// </summary>
public interface IDocumentProcessor
{
    Task ProcessAsync(Guid documentId, CancellationToken cancellationToken = default);
}
