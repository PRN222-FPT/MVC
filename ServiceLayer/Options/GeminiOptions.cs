namespace ServiceLayer.Options;

/// <summary>
/// Options for configuring the Gemini model client.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// Chat provider to use for answer generation. Supported values: "Google", "OpenRouter".
    /// </summary>
    public string ChatProvider { get; set; } = GeminiChatProviders.OpenRouter;

    /// <summary>
    /// The API key used to authenticate with the Gemini Developer API.
    /// This key is also used for Gemini embeddings.
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
    public string SystemPrompt { get; set; } = "You are an AI assistant for accurate question answering over retrieved documents.\n\nInstructions:\n* Answer ONLY using the provided context.\n* If the answer is not found in the context, reply exactly:\n\"I cannot find the answer in the provided context.\"\n* Do not make up information or use outside knowledge.\n* Answer in the same language as the user's question.\n* Start with the direct answer, then add only the key supporting details.\n* Use short headings or bullet points when they improve readability.\n* Keep the answer focused and avoid filler.\n* Target response length: 350-600 words for broad or explanatory questions, shorter when the context is limited or the question is narrow.\nContext:\n{context}\nQuestion:\n{question}\nAnswer:";

    /// <summary>
    /// OpenRouter configuration used when <see cref="ChatProvider"/> is "OpenRouter".
    /// </summary>
    public OpenRouterGeminiOptions OpenRouter { get; set; } = new();
}

public static class GeminiChatProviders
{
    public const string Google = "Google";
    public const string OpenRouter = "OpenRouter";
}

public sealed class OpenRouterGeminiOptions
{
    /// <summary>
    /// The API key used to authenticate with OpenRouter.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The OpenRouter chat completions endpoint.
    /// </summary>
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";

    /// <summary>
    /// OpenRouter model id for Gemini chat generation.
    /// </summary>
    public string ModelName { get; set; } = "google/gemini-2.5-flash";

    /// <summary>
    /// Optional site URL sent to OpenRouter for app attribution.
    /// </summary>
    public string? SiteUrl { get; set; }

    /// <summary>
    /// Optional app name sent to OpenRouter for app attribution.
    /// </summary>
    public string? AppName { get; set; }
}
