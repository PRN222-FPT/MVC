using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

/// <summary>
/// Implements recursive character chunking by splitting text based on a hierarchical
/// list of separators to preserve semantic context.
/// </summary>
public sealed class RecursiveChunkingService : IRecursiveChunkingService
{
    private readonly ChunkingOptions _options;

    public RecursiveChunkingService(IOptions<ChunkingOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<string> SplitText(string text, int? chunkSize = null, int? chunkOverlap = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        int targetSize = chunkSize ?? _options.ChunkSize;
        int targetOverlap = chunkOverlap ?? _options.ChunkOverlap;

        if (targetSize <= 0)
        {
            throw new ArgumentException("Chunk size must be greater than 0.", nameof(chunkSize));
        }

        if (targetOverlap < 0)
        {
            throw new ArgumentException("Chunk overlap cannot be negative.", nameof(chunkOverlap));
        }

        // Guard against misconfiguration: overlap must be smaller than size
        if (targetOverlap >= targetSize)
        {
            targetOverlap = targetSize - 1;
        }

        var separators = _options.Separators is { Length: > 0 }
            ? _options.Separators
            : new[] { "\r\n", "\n\n", "\n", " ", "" };

        return SplitTextRecursive(text, separators, targetSize, targetOverlap);
    }

    private List<string> SplitTextRecursive(
        string text,
        IReadOnlyList<string> separators,
        int maxChunkSize,
        int chunkOverlap)
    {
        var finalChunks = new List<string>();

        if (text.Length <= maxChunkSize)
        {
            finalChunks.Add(text);
            return finalChunks;
        }

        // Find the first separator present in the text
        string? separator = null;
        int nextSeparatorIndex = -1;

        for (int i = 0; i < separators.Count; i++)
        {
            var sep = separators[i];
            if (sep == string.Empty)
            {
                separator = sep;
                nextSeparatorIndex = i + 1;
                break;
            }

            if (text.Contains(sep))
            {
                separator = sep;
                nextSeparatorIndex = i + 1;
                break;
            }
        }

        // If no separator was found, default to character splitting (empty string)
        if (separator == null)
        {
            separator = string.Empty;
            nextSeparatorIndex = separators.Count;
        }

        // Split text
        List<string> splits;
        if (separator == string.Empty)
        {
            // Split into individual characters (strings of length 1)
            splits = text.Select(c => c.ToString()).ToList();
        }
        else
        {
            splits = text.Split(new[] { separator }, StringSplitOptions.None).ToList();
        }

        var goodSplits = new List<string>();
        var remainingSeparators = separators.Skip(nextSeparatorIndex).ToList();

        foreach (var splitPart in splits)
        {
            if (splitPart.Length <= maxChunkSize)
            {
                goodSplits.Add(splitPart);
            }
            else
            {
                // If there are accumulated good splits, group and merge them first
                if (goodSplits.Count > 0)
                {
                    var merged = GroupAndMerge(goodSplits, separator, maxChunkSize, chunkOverlap);
                    finalChunks.AddRange(merged);
                    goodSplits.Clear();
                }

                // Recursively split the oversized part using remaining separators
                var recursivelySplit = SplitTextRecursive(splitPart, remainingSeparators, maxChunkSize, chunkOverlap);
                finalChunks.AddRange(recursivelySplit);
            }
        }

        // Group and merge any remaining good splits
        if (goodSplits.Count > 0)
        {
            var merged = GroupAndMerge(goodSplits, separator, maxChunkSize, chunkOverlap);
            finalChunks.AddRange(merged);
        }

        return finalChunks;
    }

    /// <summary>
    /// Groups smaller split parts back together into chunks of size <= maxChunkSize
    /// while maintaining a target chunkOverlap.
    /// </summary>
    private List<string> GroupAndMerge(
        List<string> splits,
        string separator,
        int maxChunkSize,
        int chunkOverlap)
    {
        var chunks = new List<string>();
        if (splits.Count == 0)
        {
            return chunks;
        }

        var currentDoc = new List<string>();
        int currentLength = 0;

        foreach (var s in splits)
        {
            int separatorLength = currentDoc.Count > 0 ? separator.Length : 0;

            // If adding this part exceeds maxChunkSize, flush the current chunk
            if (currentLength + separatorLength + s.Length > maxChunkSize)
            {
                if (currentDoc.Count > 0)
                {
                    chunks.Add(string.Join(separator, currentDoc));

                    // Build overlap: backtrack from the end of currentDoc
                    var tempDoc = new List<string>();
                    int tempLength = 0;

                    for (int i = currentDoc.Count - 1; i >= 0; i--)
                    {
                        int sepLen = tempDoc.Count > 0 ? separator.Length : 0;
                        if (tempLength + sepLen + currentDoc[i].Length > chunkOverlap)
                        {
                            break;
                        }
                        tempDoc.Insert(0, currentDoc[i]);
                        tempLength += sepLen + currentDoc[i].Length;
                    }

                    currentDoc = tempDoc;
                    currentLength = tempLength;
                }
            }

            int sepLenToApply = currentDoc.Count > 0 ? separator.Length : 0;
            currentDoc.Add(s);
            currentLength += sepLenToApply + s.Length;
        }

        if (currentDoc.Count > 0)
        {
            chunks.Add(string.Join(separator, currentDoc));
        }

        return chunks;
    }
}
