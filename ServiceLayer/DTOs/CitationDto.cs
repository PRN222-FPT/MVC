namespace ServiceLayer.DTOs;

/// <summary>
/// One cited source returned to the caller after RAG retrieval.
/// Fields satisfy both acceptance criteria (doc_title, page_no, score, chunk_preview 150 chars)
/// and the ChatService pipeline (DocumentId, ChunkIndex).
/// </summary>
public sealed class CitationDto
{
    public Guid DocumentId { get; set; }
    public string DocTitle { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public int PageNo { get; set; }
    public int ChunkIndex { get; set; }
    public float Score { get; set; }
    public string ChunkPreview { get; set; } = string.Empty;  // max 150 chars, [Trang N] header stripped
}

/// <summary>
/// A chunk returned by the vector store (e.g. Qdrant) together with its similarity score.
/// </summary>
public sealed record RetrievedChunkDto(
    Guid ChunkId,
    Guid DocumentId,
    string DocTitle,
    string Content,
    int ChunkIndex,
    float Score
);
