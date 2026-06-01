using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Service implementation for retrieving relevant document chunks from the vector database using semantic search.
/// </summary>
public sealed class RetrievalService : IRetrievalService
{
    private readonly IGeminiService _geminiService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<RetrievalService> _logger;

    private const float SimilarityThreshold = 0.60f;
    private const int RawRetrieveLimit = 10;
    private const int FinalContextLimit = 5;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalService"/> class.
    /// </summary>
    public RetrievalService(
        IGeminiService geminiService,
        IQdrantService qdrantService,
        ILogger<RetrievalService> logger)
    {
        _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<RetrievalResult>> RetrieveContextAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("RetrieveContextAsync called with an empty query. Returning empty context.");
            return new List<RetrievalResult>();
        }

        try
        {
            _logger.LogInformation("Retrieving context for query: '{Query}'", query);

            // 1. Embed the search query
            var queryVector = await _geminiService.EmbedTextAsync(query, cancellationToken);

            // 2. Perform search in Qdrant with limit = 10
            var searchResults = await _qdrantService.SearchAsync(
                queryVector: queryVector,
                limit: RawRetrieveLimit,
                cancellationToken: cancellationToken
            );

            // 3. Filter by threshold >= 0.60 and take top-5
            var finalResults = searchResults
                .Where(r => r.Score >= SimilarityThreshold)
                .Take(FinalContextLimit)
                .Select(r => new RetrievalResult(
                    ChunkText: r.ChunkText,
                    Score: r.Score,
                    PageNo: r.PageNo,
                    ChunkIndex: r.ChunkIndex,
                    DocumentId: r.DocumentId
                ))
                .ToList();

            _logger.LogInformation("Semantic search returned {RawCount} raw matches. Mapped to {FinalCount} final context chunks with score >= {Threshold}",
                searchResults.Count, finalResults.Count, SimilarityThreshold);

            return finalResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve context for query '{Query}'", query);
            throw;
        }
    }
}
