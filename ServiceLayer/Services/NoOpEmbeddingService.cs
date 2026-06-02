using Microsoft.Extensions.Logging;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Fail-fast fallback used when no real embedding provider is configured.
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
        int inputCount = inputs?.Count ?? 0;
        _logger.LogError(
            "Embedding generation failed before provider call because no embedding provider is configured. Input count: {InputCount}.",
            inputCount);

        throw new InvalidOperationException(
            "No embedding provider is configured. Set Gemini:ApiKey or register a real IEmbeddingService before processing documents.");
    }
}
