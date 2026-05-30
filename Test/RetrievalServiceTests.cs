using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceLayer.Interfaces;
using ServiceLayer.Services;
using Xunit;

namespace Test;

/// <summary>
/// Unit tests for <see cref="RetrievalService"/> verifying threshold filtering and clamping behavior.
/// </summary>
public class RetrievalServiceTests
{
    private class FakeGeminiService : IGeminiService
    {
        public bool EmbedTextCalled { get; private set; }
        public string? LastEmbeddedText { get; private set; }

        public Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
        {
            EmbedTextCalled = true;
            LastEmbeddedText = text;
            return Task.FromResult(new float[] { 0.1f, 0.2f, 0.3f });
        }

        public Task<string> GenerateAnswerAsync(string context, string question, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Fake answer");
        }
    }

    private class FakeQdrantService : IQdrantService
    {
        private readonly List<QdrantSearchResult> _resultsToReturn;

        public FakeQdrantService(List<QdrantSearchResult> resultsToReturn)
        {
            _resultsToReturn = resultsToReturn;
        }

        public int LastLimit { get; private set; }
        public bool SearchCalled { get; private set; }

        public Task CreateCollectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertVectorsAsync(IEnumerable<QdrantVectorPoint> points, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<List<QdrantSearchResult>> SearchAsync(float[] queryVector, int limit = 5, CancellationToken cancellationToken = default)
        {
            SearchCalled = true;
            LastLimit = limit;
            return Task.FromResult(_resultsToReturn);
        }
    }

    [Fact]
    public async Task RetrieveContextAsync_EmptyQuery_ReturnsEmptyList()
    {
        // Arrange
        var gemini = new FakeGeminiService();
        var qdrant = new FakeQdrantService(new List<QdrantSearchResult>());
        var logger = NullLogger<RetrievalService>.Instance;
        var service = new RetrievalService(gemini, qdrant, logger);

        // Act
        var result = await service.RetrieveContextAsync("   ");

        // Assert
        Assert.Empty(result);
        Assert.False(gemini.EmbedTextCalled);
        Assert.False(qdrant.SearchCalled);
    }

    [Fact]
    public async Task RetrieveContextAsync_ValidQuery_PerformsSemanticSearchAndFiltersResults()
    {
        // Arrange
        var gemini = new FakeGeminiService();
        var docId = Guid.NewGuid();
        var searchResults = new List<QdrantSearchResult>
        {
            new QdrantSearchResult(docId, 1, "Chunk 1", 0, 0.85f),
            new QdrantSearchResult(docId, 1, "Chunk 2", 1, 0.70f),
            new QdrantSearchResult(docId, 2, "Chunk 3", 2, 0.60f), // Borderline passes threshold
            new QdrantSearchResult(docId, 2, "Chunk 4", 3, 0.59f), // Fails threshold
            new QdrantSearchResult(docId, 3, "Chunk 5", 4, 0.40f)  // Fails threshold
        };
        var qdrant = new FakeQdrantService(searchResults);
        var logger = NullLogger<RetrievalService>.Instance;
        var service = new RetrievalService(gemini, qdrant, logger);

        // Act
        var results = await service.RetrieveContextAsync("search query");

        // Assert
        Assert.True(gemini.EmbedTextCalled);
        Assert.Equal("search query", gemini.LastEmbeddedText);
        Assert.True(qdrant.SearchCalled);
        Assert.Equal(10, qdrant.LastLimit); // Check if search limit is 10
        
        // Assert filtered results: only first 3 pass threshold >= 0.60
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Score >= 0.60f));
        Assert.Equal("Chunk 1", results[0].ChunkText);
        Assert.Equal("Chunk 2", results[1].ChunkText);
        Assert.Equal("Chunk 3", results[2].ChunkText);
    }

    [Fact]
    public async Task RetrieveContextAsync_MoreThanFiveMatches_ClampsToTopFive()
    {
        // Arrange
        var gemini = new FakeGeminiService();
        var docId = Guid.NewGuid();
        var searchResults = new List<QdrantSearchResult>();
        for (int i = 1; i <= 8; i++)
        {
            searchResults.Add(new QdrantSearchResult(docId, 1, $"Chunk {i}", i - 1, 0.80f)); // All pass threshold
        }
        var qdrant = new FakeQdrantService(searchResults);
        var logger = NullLogger<RetrievalService>.Instance;
        var service = new RetrievalService(gemini, qdrant, logger);

        // Act
        var results = await service.RetrieveContextAsync("query");

        // Assert
        Assert.Equal(5, results.Count); // Capped to top-5
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal($"Chunk {i + 1}", results[i].ChunkText);
        }
    }
}
