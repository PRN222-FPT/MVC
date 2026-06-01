using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Model representing a vector point to be upserted to Qdrant.
/// </summary>
public sealed record QdrantVectorPoint(
    Guid ChunkId,
    Guid DocumentId,
    int PageNo,
    string ChunkText,
    int ChunkIndex,
    float[] Vector);

/// <summary>
/// Model representing a search result returned from Qdrant.
/// </summary>
public sealed record QdrantSearchResult(
    Guid DocumentId,
    int PageNo,
    string ChunkText,
    int ChunkIndex,
    float Score);

/// <summary>
/// Service interface for interacting with the Qdrant vector database.
/// </summary>
public interface IQdrantService
{
    /// <summary>
    /// Creates the 'documents' collection in Qdrant if it does not already exist,
    /// with Cosine distance and HNSW config.
    /// </summary>
    Task CreateCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a batch of vector points to Qdrant.
    /// </summary>
    Task UpsertVectorsAsync(IEnumerable<QdrantVectorPoint> points, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for similar vectors in the 'documents' collection.
    /// </summary>
    Task<List<QdrantSearchResult>> SearchAsync(float[] queryVector, int limit = 5, CancellationToken cancellationToken = default);
}
