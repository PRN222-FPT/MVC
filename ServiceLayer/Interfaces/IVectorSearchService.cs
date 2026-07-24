using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceLayer.Interfaces;

/// <summary>
/// A chunk match returned from a similarity search, with its cosine similarity score.
/// </summary>
public sealed record VectorSearchResult(
    Guid DocumentId,
    int PageNo,
    string ChunkText,
    int ChunkIndex,
    float Score);

/// <summary>
/// Service interface for similarity search over chunk embeddings.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Finds the chunks whose stored embedding is most similar to <paramref name="queryVector"/>.
    /// </summary>
    Task<List<VectorSearchResult>> SearchAsync(float[] queryVector, int limit = 5, CancellationToken cancellationToken = default);
}
