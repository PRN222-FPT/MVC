using Microsoft.Extensions.Logging;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Stub: returns empty vectors and logs a warning. Swap for a real provider once an
/// embedding API key is configured (e.g. OpenAI text-embedding-3-small).
/// </summary>
public sealed class NoOpEmbeddingService : IEmbeddingService
{
    private readonly ILogger<NoOpEmbeddingService> _logger;

    public NoOpEmbeddingService(ILogger<NoOpEmbeddingService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "NoOpEmbeddingService: embedding skipped for {Count} inputs — configure a real embedding provider.",
            inputs.Count);

        IReadOnlyList<float[]> result = inputs.Select(_ => Array.Empty<float>()).ToArray();
        return Task.FromResult(result);
    }
}
