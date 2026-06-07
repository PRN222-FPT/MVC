using Microsoft.Extensions.Logging;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Stub: logs warnings and does nothing. Swap for the real QdrantService (Anh Kiệt - T16)
/// once docker-compose Qdrant is running and the branch is merged.
/// </summary>
public sealed class NoOpQdrantService : IQdrantService
{
    private readonly ILogger<NoOpQdrantService> _logger;

    public NoOpQdrantService(ILogger<NoOpQdrantService> logger)
    {
        _logger = logger;
    }

    public Task CreateCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("NoOpQdrantService: CreateCollection skipped — configure Qdrant (T16).");
        return Task.CompletedTask;
    }

    public Task UpsertVectorsAsync(IEnumerable<QdrantVectorPoint> points, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("NoOpQdrantService: UpsertVectors skipped — configure Qdrant (T16).");
        return Task.CompletedTask;
    }

    public Task<List<QdrantSearchResult>> SearchAsync(float[] queryVector, int limit = 5, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("NoOpQdrantService: Search skipped — configure Qdrant (T16).");
        return Task.FromResult(new List<QdrantSearchResult>());
    }
}
