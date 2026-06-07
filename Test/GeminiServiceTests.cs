using System;
using System.Threading.Tasks;
using Google.GenAI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceLayer.Options;
using ServiceLayer.Services;
using Xunit;

namespace Test;

/// <summary>
/// Unit tests for <see cref="GeminiService"/> verifying constructor constraints and validation rules.
/// </summary>
public class GeminiServiceTests
{
    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        var options = Options.Create(new GeminiOptions());
        var logger = NullLogger<GeminiService>.Instance;

        Assert.Throws<ArgumentNullException>(() => new GeminiService(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var client = new Client(apiKey: "API_KEY");
        var logger = NullLogger<GeminiService>.Instance;

        Assert.Throws<ArgumentNullException>(() => new GeminiService(client, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var client = new Client(apiKey: "API_KEY");
        var options = Options.Create(new GeminiOptions());

        Assert.Throws<ArgumentNullException>(() => new GeminiService(client, options, null!));
    }

    [Fact]
    public async Task GenerateAnswerAsync_NullOrWhitespaceQuestion_ThrowsArgumentException()
    {
        var client = new Client(apiKey: "API_KEY");
        var options = Options.Create(new GeminiOptions());
        var logger = NullLogger<GeminiService>.Instance;
        var service = new GeminiService(client, options, logger);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAnswerAsync("context", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAnswerAsync("context", "   "));
    }
}
