using ServiceLayer.Options;
using Xunit;

namespace Test;

public sealed class GeminiOptionsTests
{
    [Fact]
    public void Defaults_UseOpenRouterForChatGeneration()
    {
        var options = new GeminiOptions();

        Assert.Equal(GeminiChatProviders.OpenRouter, options.ChatProvider);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", options.OpenRouter.BaseUrl);
        Assert.StartsWith("google/gemini-", options.OpenRouter.ModelName);
    }
}
