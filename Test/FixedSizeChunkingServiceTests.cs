using System;
using System.Collections.Generic;
using System.Linq;
using DocumentParser.Models;
using ServiceLayer.Interfaces;
using ServiceLayer.Services;
using Xunit;

namespace Test;

public class FixedSizeChunkingServiceTests
{
    private static IFixedSizeChunkingService CreateService() => new FixedSizeChunkingService();

    private static int NonNewlineLength(string s) => s.Count(c => c != '\n' && c != '\r');

    [Fact]
    public void ChunkDocument_EmptyOrWhitespacePages_ReturnsNoChunks()
    {
        var service = CreateService();
        var pages = new List<ParsedPage>
        {
            new ParsedPage(1, "", IsEmpty: true),
            new ParsedPage(2, "   ", IsEmpty: false)
        };

        var chunks = service.ChunkDocument(pages, chunkSizeCharacters: 50);

        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkDocument_TextShorterThanChunkSize_ReturnsSingleChunk()
    {
        var service = CreateService();
        string text = "Short page text.";
        var pages = new List<ParsedPage> { new ParsedPage(1, text, IsEmpty: false) };

        var chunks = service.ChunkDocument(pages, chunkSizeCharacters: 100);

        Assert.Single(chunks);
        Assert.Equal($"[Trang 1]\n{text}", chunks[0].Content);
        Assert.Equal(1, chunks[0].PageNumber);
        Assert.Equal(0, chunks[0].ChunkIndex);
    }

    [Fact]
    public void ChunkDocument_OversizedText_SplitsIntoFixedSizeSlicesWithNoOverlap()
    {
        var service = CreateService();
        // Header for page 1 is "[Trang 1]\n" -> 9 non-newline chars, so a chunkSizeCharacters
        // of 50 leaves a 41-char text budget per chunk: 105 chars -> 41/41/23.
        string text = new string('a', 105);
        var pages = new List<ParsedPage> { new ParsedPage(1, text, IsEmpty: false) };

        var chunks = service.ChunkDocument(pages, chunkSizeCharacters: 50);

        Assert.Equal(3, chunks.Count);

        string chunk0Text = chunks[0].Content["[Trang 1]\n".Length..];
        string chunk1Text = chunks[1].Content["[Trang 1]\n".Length..];
        string chunk2Text = chunks[2].Content["[Trang 1]\n".Length..];

        Assert.Equal(41, chunk0Text.Length);
        Assert.Equal(41, chunk1Text.Length);
        Assert.Equal(23, chunk2Text.Length);

        // No overlap: concatenating the raw slices reproduces the original text exactly.
        Assert.Equal(text, chunk0Text + chunk1Text + chunk2Text);
    }

    [Fact]
    public void ChunkDocument_FullChunks_NonNewlineContentLengthMatchesConfiguredChunkSize()
    {
        // Regression: the persisted/embedded Content's non-newline character count must equal
        // chunkSizeCharacters for full chunks (the header's own "\n" doesn't count either).
        var service = CreateService();
        string text = new string('a', 205); // no newlines; budget = 100 - 9 (header) = 91 -> 91/91/23
        var pages = new List<ParsedPage> { new ParsedPage(1, text, IsEmpty: false) };

        var chunks = service.ChunkDocument(pages, chunkSizeCharacters: 100);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(100, NonNewlineLength(chunks[0].Content));
        Assert.Equal(100, NonNewlineLength(chunks[1].Content));
        Assert.True(NonNewlineLength(chunks[2].Content) <= 100);
    }

    [Fact]
    public void ChunkDocument_NewlinesInText_DoNotCountTowardChunkSizeButArePreserved()
    {
        var service = CreateService();
        // 12 non-newline chars with 2 embedded newlines.
        string text = "aaaa\nbbbb\ncccc";
        var pages = new List<ParsedPage> { new ParsedPage(1, text, IsEmpty: false) };

        // Header "[Trang 1]\n" contributes 9 non-newline chars, leaving a budget of 6.
        var chunks = service.ChunkDocument(pages, chunkSizeCharacters: 15);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(15, NonNewlineLength(chunks[0].Content));
        Assert.Equal(15, NonNewlineLength(chunks[1].Content));

        // Newlines are preserved, not stripped: reassembling the raw text (minus the header)
        // reproduces the original text exactly, embedded newlines included.
        string reconstructed = string.Concat(chunks.Select(c => c.Content["[Trang 1]\n".Length..]));
        Assert.Equal(text, reconstructed);
    }

    [Fact]
    public void ChunkDocument_MultiplePages_AssignsSequentialChunkIndexAcrossPages()
    {
        var service = CreateService();
        var pages = new List<ParsedPage>
        {
            new ParsedPage(1, "Page one content.", IsEmpty: false),
            new ParsedPage(2, "Page two content.", IsEmpty: false)
        };

        var chunks = service.ChunkDocument(pages, chunkSizeCharacters: 100);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.Equal(1, chunks[0].PageNumber);
        Assert.Equal(1, chunks[1].ChunkIndex);
        Assert.Equal(2, chunks[1].PageNumber);
    }

    [Fact]
    public void ChunkDocument_InvalidChunkSize_Throws()
    {
        var service = CreateService();
        var pages = new List<ParsedPage> { new ParsedPage(1, "Some text", IsEmpty: false) };

        Assert.Throws<ArgumentException>(() => service.ChunkDocument(pages, chunkSizeCharacters: 0));
        Assert.Throws<ArgumentException>(() => service.ChunkDocument(pages, chunkSizeCharacters: -5));
    }

    [Fact]
    public void ChunkDocument_NullOrEmptyPages_ReturnsEmpty()
    {
        var service = CreateService();

        Assert.Empty(service.ChunkDocument(null!, chunkSizeCharacters: 50));
        Assert.Empty(service.ChunkDocument(new List<ParsedPage>(), chunkSizeCharacters: 50));
    }
}
