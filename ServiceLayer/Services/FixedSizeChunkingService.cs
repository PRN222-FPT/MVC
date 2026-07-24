using DocumentParser.Models;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

/// <summary>
/// Implements fixed-size chunking: text is hard-cut every N characters, with no regard
/// for word/sentence/paragraph boundaries and no overlap between chunks. Line breaks
/// (\n, \r) are not counted against the N-character budget — they stay in the chunk's
/// content but are "free", so the admin-configured size reflects visible characters only.
/// </summary>
public sealed class FixedSizeChunkingService : IFixedSizeChunkingService
{
    public IReadOnlyList<ChunkDto> ChunkDocument(IReadOnlyList<ParsedPage> pages, int chunkSizeCharacters)
    {
        if (chunkSizeCharacters <= 0)
        {
            throw new ArgumentException("Chunk size must be greater than 0.", nameof(chunkSizeCharacters));
        }

        if (pages == null || pages.Count == 0)
        {
            return Array.Empty<ChunkDto>();
        }

        var chunkDtos = new List<ChunkDto>();
        int chunkIndex = 0;

        foreach (var page in pages)
        {
            if (page.IsEmpty || string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            // The "[Trang N]" citation header is part of the persisted/embedded chunk, so its
            // non-newline characters must count against chunkSizeCharacters — otherwise every
            // stored chunk ends up longer than the size the admin configured.
            string header = $"[Trang {page.PageNumber}]\n";
            int textBudget = Math.Max(1, chunkSizeCharacters - CountNonNewlineChars(header));

            foreach (var chunkText in SplitFixedSize(page.Text, textBudget))
            {
                chunkDtos.Add(new ChunkDto(page.PageNumber, chunkIndex++, header + chunkText));
            }
        }

        return chunkDtos;
    }

    /// <summary>
    /// Slices text into consecutive pieces, each containing up to
    /// <paramref name="chunkSizeCharacters"/> non-newline characters. Newlines (\n, \r) are
    /// carried along with whichever piece they fall in but don't count against the budget.
    /// </summary>
    private static IEnumerable<string> SplitFixedSize(string text, int chunkSizeCharacters)
    {
        int start = 0;
        int counted = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\n' or '\r'))
            {
                counted++;
            }

            if (counted == chunkSizeCharacters)
            {
                yield return text[start..(i + 1)];
                start = i + 1;
                counted = 0;
            }
        }

        if (start < text.Length)
        {
            yield return text[start..];
        }
    }

    private static int CountNonNewlineChars(string text)
    {
        int count = 0;
        foreach (char c in text)
        {
            if (c is not ('\n' or '\r'))
            {
                count++;
            }
        }

        return count;
    }
}
