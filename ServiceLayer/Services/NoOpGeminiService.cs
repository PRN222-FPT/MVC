using Microsoft.Extensions.Logging;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Development fallback used when Gemini is not configured.
/// </summary>
public sealed class NoOpGeminiService : IGeminiService
{
    private readonly ILogger<NoOpGeminiService> _logger;

    public NoOpGeminiService(ILogger<NoOpGeminiService> logger)
    {
        _logger = logger;
    }

    public Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Gemini API key is not configured. Returning an empty embedding.");
        return Task.FromResult(Array.Empty<float>());
    }

    public Task<string> GenerateAnswerAsync(string context, string question, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Gemini API key is not configured. Returning a fallback chat response.");
        return Task.FromResult(
            "Gemini API key is not configured. Set Gemini:ApiKey via user-secrets or an environment variable to enable AI answers.");
    }
}
