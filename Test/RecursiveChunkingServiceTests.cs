using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;
using ServiceLayer.Services;
using Xunit;

namespace Test;

public class RecursiveChunkingServiceTests
{
    private IRecursiveChunkingService CreateService(int chunkSize = 100, int chunkOverlap = 20, string[]? separators = null)
    {
        var options = new ChunkingOptions
        {
            ChunkSize = chunkSize,
            ChunkOverlap = chunkOverlap,
            Separators = separators ?? new[] { "\r\n", "\n\n", "\n", " ", "" }
        };
        var mockOptions = Microsoft.Extensions.Options.Options.Create(options);
        return new RecursiveChunkingService(mockOptions);
    }

    [Fact]
    public void SplitText_NullOrWhiteSpace_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();

        // Act
        var resultNull = service.SplitText(null!);
        var resultEmpty = service.SplitText("   ");

        // Assert
        Assert.Empty(resultNull);
        Assert.Empty(resultEmpty);
    }

    [Fact]
    public void SplitText_ShortText_ReturnsSingleChunk()
    {
        // Arrange
        var service = CreateService(chunkSize: 100);
        string text = "Hello World! This is a test of the recursive chunking service.";

        // Act
        var chunks = service.SplitText(text);

        // Assert
        Assert.Single(chunks);
        Assert.Equal(text, chunks[0]);
    }

    [Fact]
    public void SplitText_OversizedText_SplitsOnParagraphBoundaries()
    {
        // Arrange
        // ChunkSize is 50. Each paragraph below is ~20 chars.
        var service = CreateService(chunkSize: 50, chunkOverlap: 10);
        string p1 = "First paragraph here."; // 21 chars
        string p2 = "Second paragraph.";    // 17 chars
        string p3 = "Third paragraph.";     // 16 chars
        string text = $"{p1}\n\n{p2}\n\n{p3}";

        // Act
        var chunks = service.SplitText(text);

        // Assert
        Assert.NotEmpty(chunks);
        // None of the chunks should exceed 50 characters
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 50));
        // Verify we split along paragraphs
        Assert.Contains(p1, chunks[0]);
    }

    [Fact]
    public void SplitText_OverlapIsRespected()
    {
        // Arrange
        // ChunkSize: 30, Overlap: 10. Split should occur at spaces.
        var service = CreateService(chunkSize: 30, chunkOverlap: 10);
        string text = "Alpha beta gamma delta epsilon zeta eta theta";

        // Act
        var chunks = service.SplitText(text);

        // Assert
        Assert.True(chunks.Count > 1);
        // Verify that consecutive chunks share overlapping words/characters
        for (int i = 0; i < chunks.Count - 1; i++)
        {
            var currentChunk = chunks[i];
            var nextChunk = chunks[i + 1];

            // Find overlapping text at the end of currentChunk and beginning of nextChunk
            bool hasOverlap = false;
            for (int len = 1; len <= 10; len++)
            {
                string end = currentChunk[^len..];
                if (nextChunk.StartsWith(end))
                {
                    hasOverlap = true;
                    break;
                }
            }
            Assert.True(hasOverlap, $"Chunk {i} and {i+1} do not overlap correctly. Chunk {i}: '{currentChunk}', Chunk {i+1}: '{nextChunk}'");
        }
    }

    [Fact]
    public void SplitText_InvalidParameters_ThrowsOrClamps()
    {
        // Arrange
        var service = CreateService(chunkSize: 100, chunkOverlap: 20);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.SplitText("Some text", chunkSize: -10));
        Assert.Throws<ArgumentException>(() => service.SplitText("Some text", chunkOverlap: -5));

        // When overlap >= size, it should clamp to size - 1 and still run
        var chunks = service.SplitText("Alpha beta gamma", chunkSize: 10, chunkOverlap: 15);
        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.True(c.Length <= 10));
    }

    [Fact]
    public void ChunkDocument_MultiplePages_ReturnsStructuredChunkDtos()
    {
        // Arrange
        var service = CreateService(chunkSize: 50, chunkOverlap: 10);
        var pages = new List<DocumentParser.Models.ParsedPage>
        {
            new DocumentParser.Models.ParsedPage(1, "Page one content. Very interesting text.", IsEmpty: false),
            new DocumentParser.Models.ParsedPage(2, "Page two content. Even more interesting.", IsEmpty: false)
        };

        // Act
        var chunkDtos = service.ChunkDocument(pages);

        // Assert
        Assert.NotEmpty(chunkDtos);
        Assert.All(chunkDtos, dto => {
            Assert.True(dto.PageNumber == 1 || dto.PageNumber == 2);
            Assert.Contains($"[Trang {dto.PageNumber}]", dto.Content);
            Assert.True(dto.ChunkIndex >= 0);
        });

        // Indexes should be sequential (0, 1, 2...)
        for (int i = 0; i < chunkDtos.Count; i++)
        {
            Assert.Equal(i, chunkDtos[i].ChunkIndex);
        }
    }
}
