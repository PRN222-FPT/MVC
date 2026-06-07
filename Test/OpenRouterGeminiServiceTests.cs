using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceLayer.Options;
using ServiceLayer.Services;
using Xunit;

namespace Test;

public sealed class OpenRouterGeminiServiceTests
{
    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        var options = Options.Create(new GeminiOptions());
        var logger = NullLogger<OpenRouterGeminiService>.Instance;

        Assert.Throws<ArgumentNullException>(() => new OpenRouterGeminiService(null!, options, logger));
    }

    [Fact]
    public async Task GenerateAnswerAsync_NullOrWhitespaceQuestion_ThrowsArgumentException()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAnswerAsync("context", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAnswerAsync("context", "   "));
    }

    [Fact]
    public async Task GenerateAnswerAsync_MissingOpenRouterApiKey_ThrowsInvalidOperationException()
    {
        var service = CreateService(
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            options => options.OpenRouter.ApiKey = string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAnswerAsync("context", "question"));
    }

    [Fact]
    public async Task GenerateAnswerAsync_SendsOpenRouterRequestAndReturnsAnswer()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedRequestBody = null;
        var service = CreateService(request =>
        {
            capturedRequest = request;
            capturedRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "choices": [
                    {
                      "message": {
                        "content": "OpenRouter answer"
                      }
                    }
                  ]
                }
                """)
            };
        });

        string answer = await service.GenerateAnswerAsync("retrieved context", "What is this?");

        Assert.Equal("OpenRouter answer", answer);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://openrouter.test/api/v1/chat/completions", capturedRequest.RequestUri?.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("OPENROUTER_KEY", capturedRequest.Headers.Authorization?.Parameter);
        Assert.True(capturedRequest.Headers.Contains("HTTP-Referer"));
        Assert.True(capturedRequest.Headers.Contains("X-Title"));

        Assert.NotNull(capturedRequestBody);
        using JsonDocument json = JsonDocument.Parse(capturedRequestBody);
        Assert.Equal("google/gemini-2.5-flash", json.RootElement.GetProperty("model").GetString());
        string prompt = json.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
        Assert.Contains("retrieved context", prompt);
        Assert.Contains("What is this?", prompt);
    }

    private static OpenRouterGeminiService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        Action<GeminiOptions>? configure = null)
    {
        var options = new GeminiOptions
        {
            ChatProvider = GeminiChatProviders.OpenRouter,
            SystemPrompt = "Context: {context}\nQuestion: {question}",
            OpenRouter = new OpenRouterGeminiOptions
            {
                ApiKey = "OPENROUTER_KEY",
                BaseUrl = "https://openrouter.test/api/v1/chat/completions",
                ModelName = "google/gemini-2.5-flash",
                SiteUrl = "https://example.test",
                AppName = "MVC Test"
            }
        };
        configure?.Invoke(options);

        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
        return new OpenRouterGeminiService(
            httpClient,
            Options.Create(options),
            NullLogger<OpenRouterGeminiService>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
