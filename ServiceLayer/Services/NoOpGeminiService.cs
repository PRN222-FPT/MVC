using Microsoft.Extensions.Logging;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Development fallback used when chat generation is not configured.
/// </summary>
public sealed class NoOpGeminiService : IGeminiService
{
    private readonly ILogger<NoOpGeminiService> _logger;

    public NoOpGeminiService(ILogger<NoOpGeminiService> logger)
    {
        _logger = logger;
    }

    public Task<string> GenerateAnswerAsync(string context, string question, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Chat generation provider API key is not configured. Returning a fallback chat response.");
        return Task.FromResult(
            "Chat generation is not configured. Set Gemini:OpenRouter:ApiKey for OpenRouter chat, or Gemini:ApiKey for Google chat, via user-secrets or an environment variable to enable AI answers.");
    }
}
