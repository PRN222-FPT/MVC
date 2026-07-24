using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Similarity search over chunk embeddings stored in the "chunks.embedding" pgvector column.
/// </summary>
public sealed class PgVectorService : IVectorSearchService
{
    private readonly Prn222Context _context;
    private readonly ILogger<PgVectorService> _logger;

    public PgVectorService(Prn222Context context, ILogger<PgVectorService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<VectorSearchResult>> SearchAsync(
        float[] queryVector,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (queryVector is null)
        {
            throw new ArgumentNullException(nameof(queryVector));
        }

        if (queryVector.Length == 0)
        {
            throw new ArgumentException("Query vector must not be empty.", nameof(queryVector));
        }

        var query = new Vector(queryVector);

        try
        {
            _logger.LogInformation("Searching similar chunk embeddings with limit {Limit}...", limit);

            var matches = await _context.Chunks
                .Where(c => c.Embedding != null)
                .OrderBy(c => c.Embedding!.CosineDistance(query))
                .Take(limit)
                .Select(c => new
                {
                    c.DocumentId,
                    c.Content,
                    c.ChunkIndex,
                    Distance = c.Embedding!.CosineDistance(query)
                })
                .ToListAsync(cancellationToken);

            var results = matches
                .Select(m => new VectorSearchResult(
                    DocumentId: m.DocumentId,
                    PageNo: CitationService.ExtractPageNumber(m.Content),
                    ChunkText: m.Content,
                    ChunkIndex: m.ChunkIndex,
                    Score: 1f - (float)m.Distance))
                .ToList();

            _logger.LogInformation("Vector search completed. Found {Count} matching chunks.", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search chunk embeddings.");
            throw;
        }
    }
}
