using System.Threading;
using System.Threading.Tasks;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Service interface for interacting with the Google Generative AI Gemini models.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Generates a semantic vector embedding for the given text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A float array representing the vector embedding.</returns>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an answer to the given question based on the provided context.
    /// </summary>
    /// <param name="context">The context/document text to use for answering.</param>
    /// <param name="question">The question to answer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response text.</returns>
    Task<string> GenerateAnswerAsync(string context, string question, CancellationToken cancellationToken = default);
}
