namespace ServiceLayer.Interfaces;

/// <summary>
/// In-process queue of document ids awaiting background processing.
/// The upload request is the producer; a hosted worker is the consumer.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>Adds a document id to the processing queue.</summary>
    ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Awaits and removes the next document id (blocks until one is available).</summary>
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}
