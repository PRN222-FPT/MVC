using System.Threading;
using System.Threading.Tasks;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Service interface for interacting with Gemini-compatible chat models.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Generates an answer to the given question based on the provided context.
    /// </summary>
    /// <param name="context">The context/document text to use for answering.</param>
    /// <param name="question">The question to answer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated response text.</returns>
    Task<string> GenerateAnswerAsync(string context, string question, CancellationToken cancellationToken = default);
}
