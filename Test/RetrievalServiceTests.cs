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
    private class FakeEmbeddingService : IEmbeddingService
    {
        public bool CreateEmbeddingsCalled { get; private set; }
        public IReadOnlyList<string>? LastInputs { get; private set; }

        public Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            CreateEmbeddingsCalled = true;
            LastInputs = inputs;
            IReadOnlyList<float[]> result = [new float[] { 0.1f, 0.2f, 0.3f }];
            return Task.FromResult(result);
        }
    }

    private class FakeVectorSearchService : IVectorSearchService
    {
        private readonly List<VectorSearchResult> _resultsToReturn;

        public FakeVectorSearchService(List<VectorSearchResult> resultsToReturn)
        {
            _resultsToReturn = resultsToReturn;
        }

        public int LastLimit { get; private set; }
        public bool SearchCalled { get; private set; }

        public Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, int limit = 5, CancellationToken cancellationToken = default)
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
        var embedding = new FakeEmbeddingService();
        var vectorSearchService = new FakeVectorSearchService(new List<VectorSearchResult>());
        var logger = NullLogger<RetrievalService>.Instance;
        var service = new RetrievalService(embedding, vectorSearchService, logger);

        // Act
        var result = await service.RetrieveContextAsync("   ");

        // Assert
        Assert.Empty(result);
        Assert.False(embedding.CreateEmbeddingsCalled);
        Assert.False(vectorSearchService.SearchCalled);
    }

    [Fact]
    public async Task RetrieveContextAsync_ValidQuery_PerformsSemanticSearchAndFiltersResults()
    {
        // Arrange
        var embedding = new FakeEmbeddingService();
        var docId = Guid.NewGuid();
        var searchResults = new List<VectorSearchResult>
        {
            new VectorSearchResult(docId, 1, "Chunk 1", 0, 0.85f),
            new VectorSearchResult(docId, 1, "Chunk 2", 1, 0.70f),
            new VectorSearchResult(docId, 2, "Chunk 3", 2, 0.65f), // Borderline passes threshold
            new VectorSearchResult(docId, 2, "Chunk 4", 3, 0.64f), // Passes expansion threshold
            new VectorSearchResult(docId, 3, "Chunk 5", 4, 0.57f), // Fails expansion threshold
            new VectorSearchResult(docId, 3, "Chunk 5", 4, 0.40f)  // Fails threshold
        };
        var vectorSearchService = new FakeVectorSearchService(searchResults);
        var logger = NullLogger<RetrievalService>.Instance;
        var service = new RetrievalService(embedding, vectorSearchService, logger);

        // Act
        var results = await service.RetrieveContextAsync("search query");

        // Assert
        Assert.True(embedding.CreateEmbeddingsCalled);
        Assert.NotNull(embedding.LastInputs);
        Assert.Equal("search query", embedding.LastInputs[0]);
        Assert.True(vectorSearchService.SearchCalled);
        Assert.Equal(30, vectorSearchService.LastLimit);
        
        // Strong matches select the document; context expansion keeps additional
        // same-document chunks down to the lower expansion threshold.
        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.True(r.Score >= 0.58f));
        Assert.Equal("Chunk 1", results[0].ChunkText);
        Assert.Equal("Chunk 2", results[1].ChunkText);
        Assert.Equal("Chunk 3", results[2].ChunkText);
        Assert.Equal("Chunk 4", results[3].ChunkText);
    }

    [Fact]
    public async Task RetrieveContextAsync_OverviewQuestion_ExpandsSearchQueryWithGeneralContextTerms()
    {
        // Arrange
        var embedding = new FakeEmbeddingService();
        var docId = Guid.NewGuid();
        var searchResults = new List<VectorSearchResult>
        {
            new VectorSearchResult(docId, 1, "PRN222 course overview", 0, 0.85f)
        };
        var vectorSearchService = new FakeVectorSearchService(searchResults);
        var service = new RetrievalService(embedding, vectorSearchService, NullLogger<RetrievalService>.Instance);

        // Act
        var results = await service.RetrieveContextAsync("Introduce PRN222 to me");

        // Assert
        Assert.Single(results);
        Assert.NotNull(embedding.LastInputs);
        string retrievalQuery = Assert.Single(embedding.LastInputs);
        Assert.Contains("Introduce PRN222 to me", retrievalQuery);
        Assert.Contains("introduction", retrievalQuery);
        Assert.Contains("overview", retrievalQuery);
        Assert.Contains("learning objectives", retrievalQuery);
    }

    [Fact]
    public async Task RetrieveContextAsync_TellMeAboutQuestion_ExpandsSearchQueryWithoutSubjectSpecificHardCoding()
    {
        // Arrange
        var embedding = new FakeEmbeddingService();
        var docId = Guid.NewGuid();
        var searchResults = new List<VectorSearchResult>
        {
            new VectorSearchResult(docId, 1, "Authentication overview", 0, 0.85f)
        };
        var vectorSearchService = new FakeVectorSearchService(searchResults);
        var service = new RetrievalService(embedding, vectorSearchService, NullLogger<RetrievalService>.Instance);

        // Act
        var results = await service.RetrieveContextAsync("Tell me about authentication");

        // Assert
        Assert.Single(results);
        Assert.NotNull(embedding.LastInputs);
        string retrievalQuery = Assert.Single(embedding.LastInputs);
        Assert.Contains("Tell me about authentication", retrievalQuery);
        Assert.Contains("description", retrievalQuery);
        Assert.DoesNotContain("PRN222", retrievalQuery);
    }

    [Fact]
    public async Task RetrieveContextAsync_NonPrn222Question_UsesOriginalSearchQuery()
    {
        // Arrange
        var embedding = new FakeEmbeddingService();
        var vectorSearchService = new FakeVectorSearchService(new List<VectorSearchResult>());
        var service = new RetrievalService(embedding, vectorSearchService, NullLogger<RetrievalService>.Instance);

        // Act
        await service.RetrieveContextAsync("What is the grading policy?");

        // Assert
        Assert.NotNull(embedding.LastInputs);
        string retrievalQuery = Assert.Single(embedding.LastInputs);
        Assert.Equal("What is the grading policy?", retrievalQuery);
    }

    [Fact]
    public async Task RetrieveContextAsync_MoreThanSixMatches_ClampsToTopSix()
    {
        // Arrange
        var embedding = new FakeEmbeddingService();
        var docId = Guid.NewGuid();
        var searchResults = new List<VectorSearchResult>();
        for (int i = 1; i <= 8; i++)
        {
            searchResults.Add(new VectorSearchResult(docId, 1, $"Chunk {i}", i - 1, 0.80f)); // All pass threshold
        }
        var vectorSearchService = new FakeVectorSearchService(searchResults);
        var logger = NullLogger<RetrievalService>.Instance;
        var service = new RetrievalService(embedding, vectorSearchService, logger);

        // Act
        var results = await service.RetrieveContextAsync("query");

        // Assert
        Assert.Equal(6, results.Count); // Capped to top-6
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal($"Chunk {i + 1}", results[i].ChunkText);
        }
    }

    [Fact]
    public async Task RetrieveContextAsync_NormalQuestion_ExpandsContextWithinDominantDocument()
    {
        // Arrange
        var embedding = new FakeEmbeddingService();
        var dominantDocId = Guid.NewGuid();
        var competingDocId = Guid.NewGuid();
        var searchResults = new List<VectorSearchResult>
        {
            new VectorSearchResult(dominantDocId, 1, "Strong dominant chunk", 0, 0.88f),
            new VectorSearchResult(dominantDocId, 2, "Expanded dominant chunk", 1, 0.59f),
            new VectorSearchResult(competingDocId, 1, "Competing expanded chunk", 0, 0.58f)
        };
        var vectorSearchService = new FakeVectorSearchService(searchResults);
        var service = new RetrievalService(embedding, vectorSearchService, NullLogger<RetrievalService>.Instance);

        // Act
        var results = await service.RetrieveContextAsync("What are the requirements?");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(dominantDocId, result.DocumentId));
        Assert.Contains(results, result => result.ChunkText == "Expanded dominant chunk");
    }

    [Fact]
    public async Task RetrieveContextAsync_NormalQuestion_PrefersDominantDocument()
    {
        // Arrange
        var embedding = new FakeEmbeddingService();
        var dominantDocId = Guid.NewGuid();
        var competingDocId = Guid.NewGuid();
        var searchResults = new List<VectorSearchResult>
        {
            new VectorSearchResult(competingDocId, 1, "Competing top chunk", 0, 0.91f),
            new VectorSearchResult(dominantDocId, 1, "Dominant chunk 1", 0, 0.89f),
            new VectorSearchResult(dominantDocId, 2, "Dominant chunk 2", 1, 0.88f),
            new VectorSearchResult(dominantDocId, 3, "Dominant chunk 3", 2, 0.87f),
            new VectorSearchResult(competingDocId, 2, "Competing weaker chunk", 1, 0.66f)
        };
        var vectorSearchService = new FakeVectorSearchService(searchResults);
        var service = new RetrievalService(embedding, vectorSearchService, NullLogger<RetrievalService>.Instance);

        // Act
        var results = await service.RetrieveContextAsync("What is the grading policy?");

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, result => Assert.Equal(dominantDocId, result.DocumentId));
    }

    [Fact]
    public async Task RetrieveContextAsync_ComparisonQuestion_AllowsMultipleDocuments()
    {
        // Arrange
        var embedding = new FakeEmbeddingService();
        var firstDocId = Guid.NewGuid();
        var secondDocId = Guid.NewGuid();
        var searchResults = new List<VectorSearchResult>
        {
            new VectorSearchResult(firstDocId, 1, "First document chunk", 0, 0.90f),
            new VectorSearchResult(secondDocId, 1, "Second document chunk", 0, 0.88f)
        };
        var vectorSearchService = new FakeVectorSearchService(searchResults);
        var service = new RetrievalService(embedding, vectorSearchService, NullLogger<RetrievalService>.Instance);

        // Act
        var results = await service.RetrieveContextAsync("Compare these documents");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.DocumentId == firstDocId);
        Assert.Contains(results, result => result.DocumentId == secondDocId);
    }
}
