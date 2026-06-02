using System;
using System.Threading.Tasks;
using Google.GenAI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceLayer.Options;
using ServiceLayer.Services;
using Xunit;

namespace Test;

public sealed class GeminiEmbeddingServiceValidationTests
{
    [Fact]
    public async Task NoOpEmbeddingService_CreateEmbeddingsAsync_ThrowsClearConfigurationError()
    {
        var service = new NoOpEmbeddingService(NullLogger<NoOpEmbeddingService>.Instance);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateEmbeddingsAsync(["valid text"]));

        Assert.Contains("No embedding provider is configured", exception.Message);
        Assert.Contains("Gemini:ApiKey", exception.Message);
    }

    [Fact]
    public async Task GeminiEmbeddingService_CreateEmbeddingsAsync_WhitespaceInput_ThrowsArgumentException()
    {
        var client = new Client(apiKey: "API_KEY");
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "API_KEY",
            EmbeddingModelName = "gemini-embedding-001"
        });
        var qdrantOptions = Options.Create(new QdrantOptions { VectorSize = 3072 });
        var service = new GeminiEmbeddingService(client, options, qdrantOptions, NullLogger<GeminiEmbeddingService>.Instance);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateEmbeddingsAsync(["valid text", "   "]));

        Assert.Contains("whitespace-only", exception.Message);
    }
}
