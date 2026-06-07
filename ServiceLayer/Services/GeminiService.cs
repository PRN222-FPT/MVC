using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

/// <summary>
/// Service implementation for interacting with the Google Generative AI Gemini API.
/// </summary>
public sealed class GeminiService : IGeminiService
{
    private readonly Client _client;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiService"/> class.
    /// </summary>
    public GeminiService(
        Client client,
        IOptions<GeminiOptions> options,
        ILogger<GeminiService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> GenerateAnswerAsync(string context, string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question cannot be null or whitespace.", nameof(question));
        }

        try
        {
            _logger.LogInformation("Generating answer for question using model {ModelName}...", _options.ModelName);
            
            string contents = BuildPrompt(context, question);

            var response = await _client.Models.GenerateContentAsync(
                model: _options.ModelName,
                contents: new Content
                {
                    Parts = new List<Part> { new Part { Text = contents } }
                },
                config: new GenerateContentConfig
                {
                    SystemInstruction = null
                },
                cancellationToken: cancellationToken
            );

            string? answer = response.Candidates?[0]?.Content?.Parts?[0]?.Text;
            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new InvalidOperationException("Failed to generate response: empty content returned from Gemini.");
            }

            _logger.LogInformation("Successfully generated answer of length {Length} characters.", answer.Length);
            return answer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate answer using model '{ModelName}'", _options.ModelName);
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
}
