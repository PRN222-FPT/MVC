using ServiceLayer.Interfaces;

namespace MVC.Workers;

/// <summary>
/// Long-running hosted service: dequeues document ids and processes each one in its own
/// DI scope (so it can use scoped services like the DbContext). Errors are logged and
/// swallowed so one bad document never stops the worker loop.
/// </summary>
public sealed class DocumentProcessingWorker : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        IBackgroundTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document processing worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid documentId;
            try
            {
                documentId = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // shutting down
            }

            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDocumentProcessor>();
                await processor.ProcessAsync(documentId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing document {DocumentId}", documentId);
            }
        }

        _logger.LogInformation("Document processing worker stopping");
    }
}
