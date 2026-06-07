using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Model representing a retrieved document chunk with its metadata and similarity score.
/// </summary>
public sealed record RetrievalResult(
    string ChunkText,
    float Score,
    int PageNo,
    int ChunkIndex,
    Guid DocumentId);

/// <summary>
/// Service interface for retrieving relevant document context using semantic search.
/// </summary>
public interface IRetrievalService
{
    /// <summary>
    /// Embeds the search query, performs a vector search against Qdrant (top-10),
    /// filters results based on similarity threshold (>= 0.60), and returns the top-5.
    /// </summary>
    /// <param name="query">The search query/question.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching document chunks.</returns>
    Task<List<RetrievalResult>> RetrieveContextAsync(string query, CancellationToken cancellationToken = default);
}
