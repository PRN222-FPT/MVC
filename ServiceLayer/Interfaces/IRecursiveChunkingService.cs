using System.Collections.Generic;
using DocumentParser.Models;
using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Service for breaking document text into smaller chunks recursively.
/// </summary>
public interface IRecursiveChunkingService
{
    /// <summary>
    /// Splits the given text into chunks using recursive character splitting.
    /// </summary>
    /// <param name="text">The source text to split.</param>
    /// <param name="chunkSize">Optional chunk size (characters) to override options.</param>
    /// <param name="chunkOverlap">Optional chunk overlap (characters) to override options.</param>
    /// <returns>A list of text chunks.</returns>
    IReadOnlyList<string> SplitText(string text, int? chunkSize = null, int? chunkOverlap = null);

    /// <summary>
    /// Chunks a collection of parsed document pages into structured Chunk DTOs.
    /// </summary>
    IReadOnlyList<ChunkDto> ChunkDocument(
        IReadOnlyList<ParsedPage> pages,
        int? chunkSize = null,
        int? chunkOverlap = null);
}
