namespace ServiceLayer.Options;

/// <summary>
/// Options for configuring the Gemini model client.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// The API key used to authenticate with the Gemini Developer API.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The model name for text generation/answering.
    /// Default is "gemini-2.0-flash-lite".
    /// </summary>
    public string ModelName { get; set; } = "gemini-2.0-flash-lite";

    /// <summary>
    /// The model name for vector embeddings.
    /// Default is "gemini-embedding-001" with full-size 3072-dimensional output.
    /// </summary>
    public string EmbeddingModelName { get; set; } = "gemini-embedding-001";

    /// <summary>
    /// System instruction/prompt used to guide response generation.
    /// </summary>
    public string SystemPrompt { get; set; } = "You are a helpful and precise assistant. Use the provided context to answer the user's question accurately. Keep your response within 300 words. If the context does not contain the answer, say that you cannot find the answer in the provided context.";
}
