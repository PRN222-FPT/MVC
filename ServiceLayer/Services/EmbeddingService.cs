using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

public sealed class EmbeddingService : IEmbeddingService
{
    private const int MaxBatchSize = 100;
    private const int MaxRetries = 3;

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs is null)
            throw new ArgumentNullException(nameof(inputs));

        if (inputs.Count == 0)
            return Array.Empty<float[]>();

        var results = new List<float[]>(inputs.Count);

        for (int offset = 0; offset < inputs.Count; offset += MaxBatchSize)
        {
            var batch = inputs.Skip(offset).Take(MaxBatchSize).ToArray();
            var batchResult = await CreateBatchAsync(batch, cancellationToken);
            results.AddRange(batchResult);
        }

        return results;
    }

    private async Task<IReadOnlyList<float[]>> CreateBatchAsync(
        string[] batch,
        CancellationToken cancellationToken)
    {
        var request = new EmbeddingRequest(_options.EmbeddingModel, batch);

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "embeddings")
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(request, JsonSerializerOptions()),
                        Encoding.UTF8,
                        "application/json")
                };

                using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken);
                string payload = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Embedding request failed with status {StatusCode}: {Body}", response.StatusCode, payload);
                    await DelayForRetryAsync(attempt, cancellationToken);
                    continue;
                }

                var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(
                    payload,
                    JsonSerializerOptions());

                if (embeddingResponse is null || embeddingResponse.Data.Count == 0)
                {
                    throw new InvalidOperationException("OpenAI embedding response was empty.");
                }

                if (embeddingResponse.Usage is not null)
                {
                    _logger.LogInformation("Embedding tokens used: {TotalTokens}", embeddingResponse.Usage.TotalTokens);
                }

                return embeddingResponse.Data
                    .OrderBy(item => item.Index)
                    .Select(item => item.Embedding)
                    .ToArray();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding request attempt {Attempt} failed", attempt);
                await DelayForRetryAsync(attempt, cancellationToken);
            }
        }

        throw new InvalidOperationException("Embedding request failed after retries.");
    }

    private static async Task DelayForRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        int delayMs = (int)Math.Pow(2, attempt) * 500;
        await Task.Delay(delayMs, cancellationToken);
    }

    private static JsonSerializerOptions JsonSerializerOptions() =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

    private sealed record EmbeddingRequest(string Model, IReadOnlyList<string> Input);

    private sealed record EmbeddingResponse(
        IReadOnlyList<EmbeddingData> Data,
        EmbeddingUsage? Usage);

    private sealed record EmbeddingData(int Index, float[] Embedding);

    private sealed record EmbeddingUsage(int TotalTokens);
}
