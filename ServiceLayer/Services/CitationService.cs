using System.Text.RegularExpressions;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Pure mapper: converts scored retrieved chunks into CitationDtos.
/// Has no external dependencies and is trivially unit-testable.
/// </summary>
public sealed class CitationService : ICitationService
{
    // Matches the "[Trang N]" header written by RecursiveChunkingService.ChunkDocument (line 48).
    // If that format string ever changes, update this pattern to match.
    private static readonly Regex PageTagPattern =
        new(@"\[Trang (\d+)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const int PreviewMaxLength = 150;

    public List<CitationDto> BuildCitations(IReadOnlyList<RetrievedChunkDto> retrievedChunks)
    {
        ArgumentNullException.ThrowIfNull(retrievedChunks);

        return retrievedChunks
            .Select(chunk => new CitationDto
            {
                DocumentId = chunk.DocumentId,
                DocTitle = chunk.DocTitle,
                PageNo = ExtractPageNumber(chunk.Content),
                ChunkIndex = chunk.ChunkIndex,
                Score = chunk.Score,
                ChunkPreview = BuildPreview(chunk.Content)
            })
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    internal static int ExtractPageNumber(string content)
    {
        Match match = PageTagPattern.Match(content);
        return match.Success && int.TryParse(match.Groups[1].Value, out int page) ? page : 0;
    }

    internal static string BuildPreview(string content)
    {
        // Strip the "[Trang N]" header so the preview shows only the document text.
        string stripped = PageTagPattern.Replace(content, string.Empty).TrimStart('\n', '\r', ' ');
        return stripped.Length <= PreviewMaxLength ? stripped : stripped[..PreviewMaxLength];
    }
}
