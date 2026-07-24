using DocumentParser.Models;
using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Splits document text into non-overlapping, fixed-length chunks measured in characters.
/// </summary>
public interface IFixedSizeChunkingService
{
    /// <summary>
    /// Chunks a collection of parsed document pages into structured Chunk DTOs, hard-cutting
    /// each page's text every <paramref name="chunkSizeCharacters"/> characters.
    /// </summary>
    IReadOnlyList<ChunkDto> ChunkDocument(IReadOnlyList<ParsedPage> pages, int chunkSizeCharacters);
}
