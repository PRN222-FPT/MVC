using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private const int MaxRetries = 3;

    private readonly Client _client;
    private readonly GeminiOptions _options;
    private readonly VectorStoreOptions _vectorStoreOptions;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(
        Client client,
        IOptions<GeminiOptions> options,
        IOptions<VectorStoreOptions> vectorStoreOptions,
        ILogger<GeminiEmbeddingService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _vectorStoreOptions = vectorStoreOptions?.Value ?? throw new ArgumentNullException(nameof(vectorStoreOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs is null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        if (inputs.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        int invalidInputCount = inputs.Count(string.IsNullOrWhiteSpace);
        if (invalidInputCount > 0)
        {
            throw new ArgumentException(
                $"Embedding inputs contain {invalidInputCount} null, empty, or whitespace-only item(s).",
                nameof(inputs));
        }

        _logger.LogInformation(
            "Creating Gemini embeddings using model {EmbeddingModel} for {InputCount} inputs. Requested dimension: {EmbeddingDimension}.",
            _options.EmbeddingModelName,
            inputs.Count,
            _vectorStoreOptions.VectorSize);

        var embeddings = new List<float[]>(inputs.Count);
        for (int i = 0; i < inputs.Count; i++)
        {
            float[] embedding = await CreateEmbeddingWithRetryAsync(inputs[i], i, cancellationToken);
            embeddings.Add(embedding);
        }

        _logger.LogInformation(
            "Gemini embedding generation completed. Count: {EmbeddingCount}; dimension: {EmbeddingDimension}.",
            embeddings.Count,
            embeddings[0].Length);

        return embeddings;
    }

    private async Task<float[]> CreateEmbeddingWithRetryAsync(
        string input,
        int inputIndex,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await _client.Models.EmbedContentAsync(
                    model: _options.EmbeddingModelName,
                    contents: input,
                    config: new EmbedContentConfig
                    {
                        OutputDimensionality = _vectorStoreOptions.VectorSize
                    },
                    cancellationToken: cancellationToken);

                float[]? values = response.Embeddings is { Count: > 0 }
                    ? response.Embeddings[0]?.Values?.Select(value => (float)value).ToArray()
                    : null;

                ValidateEmbedding(values, inputIndex, _vectorStoreOptions.VectorSize);
                return values!;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsTransientEmbeddingFailure(ex) && attempt < MaxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "Gemini embedding request attempt {Attempt} for model {EmbeddingModel} failed with a transient error. Input index: {InputIndex}.",
                    attempt,
                    _options.EmbeddingModelName,
                    inputIndex);
                await DelayForRetryAsync(attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Gemini embedding request failed for model {EmbeddingModel}. Input index: {InputIndex}.",
                    _options.EmbeddingModelName,
                    inputIndex);
                throw;
            }
        }

        throw new InvalidOperationException(
            $"Gemini embedding request for model '{_options.EmbeddingModelName}' failed after {MaxRetries} attempts.");
    }

    private static void ValidateEmbedding(float[]? embedding, int inputIndex, int expectedDimension)
    {
        if (embedding is null || embedding.Length == 0)
        {
            throw new InvalidOperationException($"Gemini embedding response item {inputIndex} was null or empty.");
        }

        if (embedding.Length != expectedDimension)
        {
            throw new InvalidOperationException(
                $"Gemini embedding response item {inputIndex} dimension {embedding.Length} does not match expected dimension {expectedDimension}.");
        }
    }

    private static bool IsTransientEmbeddingFailure(Exception exception)
    {
        string text = exception.ToString();
        return text.Contains("429", StringComparison.OrdinalIgnoreCase)
            || text.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
            || text.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
            || text.Contains("500", StringComparison.OrdinalIgnoreCase)
            || text.Contains("502", StringComparison.OrdinalIgnoreCase)
            || text.Contains("503", StringComparison.OrdinalIgnoreCase)
            || text.Contains("504", StringComparison.OrdinalIgnoreCase)
            || text.Contains("InternalServerError", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ServiceUnavailable", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task DelayForRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        int delayMs = (int)Math.Pow(2, attempt) * 500;
        await Task.Delay(delayMs, cancellationToken);
    }
}
