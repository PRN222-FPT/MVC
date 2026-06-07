using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

/// <summary>
/// Gemini chat service routed through OpenRouter's OpenAI-compatible chat completions API.
/// </summary>
public sealed class OpenRouterGeminiService : IGeminiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<OpenRouterGeminiService> _logger;

    public OpenRouterGeminiService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<OpenRouterGeminiService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GenerateAnswerAsync(
        string context,
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question cannot be null or whitespace.", nameof(question));
        }

        OpenRouterGeminiOptions openRouter = _options.OpenRouter;
        if (string.IsNullOrWhiteSpace(openRouter.ApiKey))
        {
            throw new InvalidOperationException("Gemini:OpenRouter:ApiKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(openRouter.ModelName))
        {
            throw new InvalidOperationException("Gemini:OpenRouter:ModelName is not configured.");
        }

        if (string.IsNullOrWhiteSpace(openRouter.BaseUrl) ||
            !Uri.TryCreate(openRouter.BaseUrl, UriKind.Absolute, out Uri? endpoint))
        {
            throw new InvalidOperationException("Gemini:OpenRouter:BaseUrl must be an absolute URL.");
        }

        try
        {
            _logger.LogInformation(
                "Generating answer through OpenRouter using model {ModelName}...",
                openRouter.ModelName);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new OpenRouterChatRequest(
                    openRouter.ModelName,
                    new[]
                    {
                        new OpenRouterMessage("user", BuildPrompt(context, question))
                    }))
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openRouter.ApiKey);
            if (!string.IsNullOrWhiteSpace(openRouter.SiteUrl))
            {
                request.Headers.TryAddWithoutValidation("HTTP-Referer", openRouter.SiteUrl);
            }

            if (!string.IsNullOrWhiteSpace(openRouter.AppName))
            {
                request.Headers.TryAddWithoutValidation("X-Title", openRouter.AppName);
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "OpenRouter request failed with status {StatusCode}. Response body length: {BodyLength}.",
                    response.StatusCode,
                    responseBody.Length);
                throw new InvalidOperationException(
                    $"OpenRouter request failed with status {(int)response.StatusCode} ({response.StatusCode}).");
            }

            OpenRouterChatResponse? completion = JsonSerializer.Deserialize<OpenRouterChatResponse>(
                responseBody,
                JsonOptions);
            string? answer = completion?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new InvalidOperationException("Failed to generate response: empty content returned from OpenRouter.");
            }

            _logger.LogInformation("Successfully generated OpenRouter answer of length {Length} characters.", answer.Length);
            return answer;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate answer through OpenRouter model '{ModelName}'", openRouter.ModelName);
            throw;
        }
    }

    private string BuildPrompt(string context, string question)
    {
        if (_options.SystemPrompt.Contains("{context}", StringComparison.OrdinalIgnoreCase) ||
            _options.SystemPrompt.Contains("{question}", StringComparison.OrdinalIgnoreCase))
        {
            return _options.SystemPrompt
                .Replace("{context}", context, StringComparison.OrdinalIgnoreCase)
                .Replace("{question}", question, StringComparison.OrdinalIgnoreCase);
        }

        return $"{_options.SystemPrompt}\n\nContext:\n{context}\n\nQuestion:\n{question}\nAnswer:";
    }

    private sealed record OpenRouterChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OpenRouterMessage> Messages);

    private sealed record OpenRouterMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenRouterChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<OpenRouterChoice>? Choices);

    private sealed record OpenRouterChoice(
        [property: JsonPropertyName("message")] OpenRouterResponseMessage? Message);

    private sealed record OpenRouterResponseMessage(
        [property: JsonPropertyName("content")] string? Content);
}
