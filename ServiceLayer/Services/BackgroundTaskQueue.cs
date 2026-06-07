using System.Threading.Channels;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Bounded, thread-safe queue backed by <see cref="Channel{T}"/>. Registered as a singleton
/// so the producer (upload request) and consumer (hosted worker) share one instance.
///
/// NOTE: in-memory only — queued items are lost on app restart. For durability across
/// restarts the ProcessingJob table (status "queued") can be used to re-enqueue on startup.
/// </summary>
public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Guid> _channel;

    public BackgroundTaskQueue(int capacity = 100)
    {
        // Wait when full so a burst of uploads applies back-pressure rather than dropping work.
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<Guid>(options);
    }

    public async ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(documentId, cancellationToken);

    public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        => await _channel.Reader.ReadAsync(cancellationToken);
}
