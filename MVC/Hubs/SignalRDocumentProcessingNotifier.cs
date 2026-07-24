using Microsoft.AspNetCore.SignalR;
using ServiceLayer.Interfaces;

namespace MVC.Hubs;

public sealed class SignalRDocumentProcessingNotifier : IDocumentProcessingNotifier
{
    private readonly IHubContext<DocumentProcessingHub> _hubContext;

    public SignalRDocumentProcessingNotifier(IHubContext<DocumentProcessingHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyProgressAsync(
        Guid documentId,
        int progressPercent,
        string stageLabel,
        CancellationToken cancellationToken = default) =>
        SendAsync(documentId, "processing", progressPercent, stageLabel, chunkCount: null, errorMessage: null, cancellationToken);

    public Task NotifyCompletedAsync(Guid documentId, int chunkCount, CancellationToken cancellationToken = default) =>
        SendAsync(documentId, "completed", 100, "Hoàn tất", chunkCount, errorMessage: null, cancellationToken);

    public Task NotifyFailedAsync(Guid documentId, string errorMessage, CancellationToken cancellationToken = default) =>
        SendAsync(documentId, "failed", 0, "Thất bại", chunkCount: null, errorMessage, cancellationToken);

    private Task SendAsync(
        Guid documentId,
        string status,
        int progressPercent,
        string stageLabel,
        int? chunkCount,
        string? errorMessage,
        CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(DocumentProcessingHub.GroupName(documentId)).SendAsync(
            DocumentProcessingHub.ClientEvent,
            new
            {
                documentId,
                status,
                progressPercent,
                stageLabel,
                chunkCount,
                errorMessage
            },
            cancellationToken);
}
