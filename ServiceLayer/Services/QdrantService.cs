using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

/// <summary>
/// Service implementation for interacting with the Qdrant vector database.
/// </summary>
public sealed class QdrantService : IQdrantService
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantService> _logger;
    private const string CollectionName = "documents";

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantService"/> class.
    /// </summary>
    public QdrantService(
        QdrantClient client,
        IOptions<QdrantOptions> options,
        ILogger<QdrantService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task CreateCollectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking if Qdrant collection '{CollectionName}' exists...", CollectionName);
            bool exists = await _client.CollectionExistsAsync(CollectionName, cancellationToken);

            if (exists)
            {
                _logger.LogInformation("Qdrant collection '{CollectionName}' already exists. Skipping creation.", CollectionName);
                return;
            }

            _logger.LogInformation("Creating Qdrant collection '{CollectionName}' with dimension {Dimension}, Distance = Cosine, and HNSW (m=16, ef_construct=100)...", 
                CollectionName, _options.VectorSize);

            await _client.CreateCollectionAsync(
                collectionName: CollectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)_options.VectorSize,
                    Distance = Distance.Cosine
                },
                hnswConfig: new HnswConfigDiff
                {
                    M = 16,
                    EfConstruct = 100
                },
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Successfully created Qdrant collection '{CollectionName}'.", CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Qdrant collection '{CollectionName}'", CollectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpsertVectorsAsync(IEnumerable<QdrantVectorPoint> points, CancellationToken cancellationToken = default)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        var pointsList = points.ToList();
        if (pointsList.Count == 0)
        {
            return;
        }

        try
        {
            var pointStructs = pointsList.Select(p => new PointStruct
            {
                Id = new PointId { Uuid = p.ChunkId.ToString() },
                Vectors = p.Vector,
                Payload =
                {
                    ["doc_id"] = p.DocumentId.ToString(),
                    ["page_no"] = p.PageNo,
                    ["chunk_text"] = p.ChunkText,
                    ["chunk_index"] = p.ChunkIndex
                }
            }).ToList();

            _logger.LogInformation("Upserting {Count} vectors into Qdrant collection '{CollectionName}'...", pointStructs.Count, CollectionName);
            await _client.UpsertAsync(collectionName: CollectionName, points: pointStructs, cancellationToken: cancellationToken);
            _logger.LogInformation("Successfully upserted {Count} vectors into Qdrant collection '{CollectionName}'.", pointStructs.Count, CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert vectors into Qdrant collection '{CollectionName}'", CollectionName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<QdrantSearchResult>> SearchAsync(float[] queryVector, int limit = 5, CancellationToken cancellationToken = default)
    {
        if (queryVector == null)
        {
            throw new ArgumentNullException(nameof(queryVector));
        }

        try
        {
            _logger.LogInformation("Searching similar vectors in Qdrant collection '{CollectionName}' with limit {Limit}...", CollectionName, limit);
            var searchResponse = await _client.SearchAsync(
                collectionName: CollectionName,
                vector: queryVector,
                limit: (ulong)limit,
                cancellationToken: cancellationToken
            );

            var results = new List<QdrantSearchResult>();
            foreach (var hit in searchResponse)
            {
                var docId = hit.Payload.TryGetValue("doc_id", out var docIdVal) && docIdVal.KindCase == Value.KindOneofCase.StringValue
                    ? Guid.Parse(docIdVal.StringValue)
                    : Guid.Empty;

                var pageNo = hit.Payload.TryGetValue("page_no", out var pageNoVal) && pageNoVal.KindCase == Value.KindOneofCase.IntegerValue
                    ? (int)pageNoVal.IntegerValue
                    : 0;

                var chunkText = hit.Payload.TryGetValue("chunk_text", out var chunkTextVal) && chunkTextVal.KindCase == Value.KindOneofCase.StringValue
                    ? chunkTextVal.StringValue
                    : string.Empty;

                var chunkIndex = hit.Payload.TryGetValue("chunk_index", out var chunkIndexVal) && chunkIndexVal.KindCase == Value.KindOneofCase.IntegerValue
                    ? (int)chunkIndexVal.IntegerValue
                    : 0;

                results.Add(new QdrantSearchResult(docId, pageNo, chunkText, chunkIndex, hit.Score));
            }

            _logger.LogInformation("Successfully completed vector search in Qdrant. Found {Count} matching documents.", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search vectors in Qdrant collection '{CollectionName}'", CollectionName);
            throw;
        }
    }
}
