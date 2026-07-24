namespace ServiceLayer.Interfaces;

/// <summary>
/// Pushes live document-processing updates to connected clients. Implemented in the MVC
/// layer (SignalR) so ServiceLayer stays free of ASP.NET Core web types.
/// </summary>
public interface IDocumentProcessingNotifier
{
    Task NotifyProgressAsync(
        Guid documentId,
        int progressPercent,
        string stageLabel,
        CancellationToken cancellationToken = default);

    Task NotifyCompletedAsync(Guid documentId, int chunkCount, CancellationToken cancellationToken = default);

    Task NotifyFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default);
}
