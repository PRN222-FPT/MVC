using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Converts pre-retrieved, scored chunks into formatted citations.
/// Retrieval itself (vector search / keyword fallback) is handled upstream.
/// </summary>
public interface ICitationService
{
    /// <summary>
    /// Maps each retrieved chunk to a <see cref="CitationDto"/> by extracting the
    /// page number from the stored content and truncating the preview to 150 chars.
    /// </summary>
    List<CitationDto> BuildCitations(IReadOnlyList<RetrievedChunkDto> retrievedChunks);
}
