namespace ServiceLayer.Options;

/// <summary>
/// Options for the pgvector-backed chunk embedding store.
/// </summary>
public sealed class VectorStoreOptions
{
    public const string SectionName = "VectorStore";

    /// <summary>
    /// Dimension of the embedding vectors stored in the "chunks.embedding" column.
    /// Must match the "vector(N)" column type and the embedding model's output dimension.
    /// Default is 3072 for Gemini's gemini-embedding-001 at full size.
    /// </summary>
    public int VectorSize { get; set; } = 3072;
}
