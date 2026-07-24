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
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly ILogger<RetrievalService> _logger;

    private const float DocumentSelectionThreshold = 0.65f;
    private const float ContextExpansionThreshold = 0.58f;
    private const int RawRetrieveLimit = 30;
    private const int FinalContextLimit = 6;
    private const int DominantDocumentCandidateLimit = 10;
    private const string OverviewRetrievalContext =
        "introduction overview summary definition description purpose scope key topics learning objectives course subject document";

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievalService"/> class.
    /// </summary>
    public RetrievalService(
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        ILogger<RetrievalService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorSearchService = vectorSearchService ?? throw new ArgumentNullException(nameof(vectorSearchService));
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

            // 1. Embed the search query with the same provider used for document chunks.
            string normalizedQuery = BuildRetrievalQuery(query);
            IReadOnlyList<float[]> queryEmbeddings = await _embeddingService.CreateEmbeddingsAsync([normalizedQuery], cancellationToken);
            float[] queryVector = queryEmbeddings.Count > 0
                ? queryEmbeddings[0]
                : Array.Empty<float>();

            if (queryVector.Length == 0)
            {
                throw new InvalidOperationException("Search query embedding was empty.");
            }

            _logger.LogInformation("Search query embedding generated with dimension {EmbeddingDimension}.", queryVector.Length);

            // 2. Retrieve a broader candidate set, then rerank for document coherence.
            var searchResults = await _vectorSearchService.SearchAsync(
                queryVector: queryVector,
                limit: RawRetrieveLimit,
                cancellationToken: cancellationToken
            );

            // 3. Use strong matches to select the right document, then expand context
            // with weaker-but-still-relevant chunks from that same document.
            var validResults = searchResults
                .Where(r => r.DocumentId != Guid.Empty)
                .ToList();

            var documentSelectionCandidates = validResults
                .Where(r => r.Score >= DocumentSelectionThreshold)
                .Take(DominantDocumentCandidateLimit)
                .ToList();

            var contextCandidates = validResults
                .Where(r => r.Score >= ContextExpansionThreshold)
                .ToList();

            IReadOnlyList<VectorSearchResult> rerankedResults = AllowsMultipleDocuments(query)
                ? contextCandidates
                : SelectDominantDocumentResults(documentSelectionCandidates, contextCandidates);

            var finalResults = rerankedResults
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.PageNo)
                .ThenBy(r => r.ChunkIndex)
                .Take(FinalContextLimit)
                .Select(r => new RetrievalResult(
                    ChunkText: r.ChunkText,
                    Score: r.Score,
                    PageNo: r.PageNo,
                    ChunkIndex: r.ChunkIndex,
                    DocumentId: r.DocumentId
                ))
                .ToList();

            _logger.LogInformation(
                "Semantic search returned {RawCount} raw matches. Selected {StrongCount} strong matches and mapped to {FinalCount} context chunks with expansion score >= {ContextThreshold}",
                searchResults.Count,
                documentSelectionCandidates.Count,
                finalResults.Count,
                ContextExpansionThreshold);

            return finalResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve context for query '{Query}'", query);
            throw;
        }
    }

    private static IReadOnlyList<VectorSearchResult> SelectDominantDocumentResults(
        IReadOnlyList<VectorSearchResult> documentSelectionCandidates,
        IReadOnlyList<VectorSearchResult> contextCandidates)
    {
        if (contextCandidates.Count == 0)
        {
            return contextCandidates;
        }

        IReadOnlyList<VectorSearchResult> rankingCandidates = documentSelectionCandidates.Count > 0
            ? documentSelectionCandidates
            : contextCandidates;

        Guid dominantDocumentId = rankingCandidates
            .GroupBy(result => result.DocumentId)
            .Select(group =>
            {
                var topScores = group
                    .OrderByDescending(result => result.Score)
                    .Take(3)
                    .Select(result => result.Score)
                    .ToList();

                float bestScore = topScores[0];
                float averageTopScore = topScores.Average();
                float supportBonus = Math.Min(group.Count(), 3) * 0.03f;

                return new
                {
                    DocumentId = group.Key,
                    Score = bestScore + averageTopScore + supportBonus
                };
            })
            .OrderByDescending(candidate => candidate.Score)
            .First()
            .DocumentId;

        return contextCandidates
            .Where(result => result.DocumentId == dominantDocumentId)
            .ToList();
    }

    private static string BuildRetrievalQuery(string query)
    {
        string trimmedQuery = query.Trim();

        return IsOverviewQuestion(trimmedQuery)
            ? $"{trimmedQuery}\n{OverviewRetrievalContext}"
            : trimmedQuery;
    }

    private static bool IsOverviewQuestion(string query)
    {
        string normalized = query.Trim().ToLowerInvariant();
        string[] overviewTerms =
        [
            "introduce",
            "introduction",
            "tell me about",
            "overview",
            "summarize",
            "summary of",
            "explain",
            "giới thiệu",
            "gioi thieu",
            "tổng quan",
            "tong quan",
            "cho tôi biết về",
            "cho toi biet ve",
            "nói về",
            "noi ve"
        ];

        return overviewTerms.Any(normalized.Contains);
    }

    private static bool AllowsMultipleDocuments(string query)
    {
        string normalized = query.Trim().ToLowerInvariant();
        string[] comparisonTerms =
        [
            "compare",
            "comparison",
            "different",
            "difference",
            "between documents",
            "across documents",
            "multiple documents",
            "so sánh",
            "khác nhau",
            "giữa các tài liệu",
            "nhiều tài liệu"
        ];

        return comparisonTerms.Any(normalized.Contains);
    }
}
