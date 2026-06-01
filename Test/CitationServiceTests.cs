using ServiceLayer.DTOs;
using ServiceLayer.Services;
using Xunit;

namespace Test;

public sealed class CitationServiceTests
{
    private readonly CitationService _sut = new();

    // ─── ExtractPageNumber ────────────────────────────────────────────────────

    [Fact]
    public void ExtractPageNumber_ReturnsParsedPage_WhenTagPresent()
    {
        string content = "[Trang 7]\nSome document text here.";
        int page = CitationService.ExtractPageNumber(content);
        Assert.Equal(7, page);
    }

    [Fact]
    public void ExtractPageNumber_ReturnsZero_WhenTagAbsent()
    {
        int page = CitationService.ExtractPageNumber("No tag in this content.");
        Assert.Equal(0, page);
    }

    [Fact]
    public void ExtractPageNumber_ReturnsFirstPage_WhenMultipleTagsPresent()
    {
        string content = "[Trang 3]\nPart one.\n[Trang 4]\nPart two.";
        Assert.Equal(3, CitationService.ExtractPageNumber(content));
    }

    // ─── BuildPreview ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildPreview_StripsPageTag_BeforeReturning()
    {
        string content = "[Trang 2]\nActual text content.";
        string preview = CitationService.BuildPreview(content);
        Assert.DoesNotContain("[Trang", preview);
        Assert.Equal("Actual text content.", preview);
    }

    [Fact]
    public void BuildPreview_TruncatesAt150Chars()
    {
        string longText = new string('a', 300);
        string content = $"[Trang 1]\n{longText}";
        string preview = CitationService.BuildPreview(content);
        Assert.Equal(150, preview.Length);
    }

    [Fact]
    public void BuildPreview_DoesNotTruncate_WhenTextUnder150()
    {
        string shortText = "Short text.";
        string content = $"[Trang 5]\n{shortText}";
        string preview = CitationService.BuildPreview(content);
        Assert.Equal(shortText, preview);
    }

    // ─── BuildCitations ───────────────────────────────────────────────────────

    [Fact]
    public void BuildCitations_ReturnsSameLengthAsInput()
    {
        var chunks = new List<RetrievedChunkDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc A", "[Trang 1]\nFirst chunk.", 0, 0.95f),
            new(Guid.NewGuid(), Guid.NewGuid(), "Doc B", "[Trang 3]\nSecond chunk.", 1, 0.80f),
        };

        List<CitationDto> citations = _sut.BuildCitations(chunks);

        Assert.Equal(2, citations.Count);
    }

    [Fact]
    public void BuildCitations_MapsFieldsCorrectly()
    {
        string content = "[Trang 12]\nImportant finding about revenue.";
        var chunk = new RetrievedChunkDto(
            ChunkId: Guid.NewGuid(),
            DocumentId: Guid.NewGuid(),
            DocTitle: "Q3 Report",
            Content: content,
            ChunkIndex: 0,
            Score: 0.92f);

        List<CitationDto> citations = _sut.BuildCitations([chunk]);

        CitationDto citation = citations[0];
        Assert.Equal("Q3 Report", citation.DocTitle);
        Assert.Equal(12, citation.PageNo);
        Assert.Equal(0.92f, citation.Score, precision: 5);
        Assert.Equal("Important finding about revenue.", citation.ChunkPreview);
    }

    [Fact]
    public void BuildCitations_ReturnsEmpty_WhenInputIsEmpty()
    {
        List<CitationDto> citations = _sut.BuildCitations([]);
        Assert.Empty(citations);
    }
}
