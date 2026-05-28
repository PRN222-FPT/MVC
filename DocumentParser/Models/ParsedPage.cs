namespace DocumentParser.Models;

/// <summary>
/// Where a page's text came from. Useful for a RAG ingestion pipeline:
/// OCR text is lower-confidence than embedded text and may need flagging downstream.
/// </summary>
public enum TextSource
{
    /// <summary>No text could be extracted (empty page, blank image, or OCR disabled).</summary>
    None,

    /// <summary>Extracted from the embedded text layer (PDF text stream / DOCX structure).</summary>
    TextLayer,

    /// <summary>Recovered from a rasterized image via OCR (Tesseract).</summary>
    Ocr
}

/// <summary>
/// Represents a single page's extracted content from a parsed document.
/// </summary>
public sealed record ParsedPage(
    int PageNumber,
    string Text,
    bool IsEmpty,
    TextSource Source = TextSource.TextLayer
)
{
    /// <summary>Human-readable preview — first 120 chars of text.</summary>
    public string Preview =>
        string.IsNullOrWhiteSpace(Text)
            ? "(no extractable text)"
            : Text.Length <= 120 ? Text : Text[..120] + "…";
}

/// <summary>
/// Aggregate result returned by any document parser.
/// </summary>
public sealed class ParseResult
{
    public required string SourceFile { get; init; }
    public required string Format { get; init; }         // "PDF-text" | "PDF-scanned" | "DOCX"
    public required IReadOnlyList<ParsedPage> Pages { get; init; }
    public int TotalPages => Pages.Count;
    public int NonEmptyPages => Pages.Count(p => !p.IsEmpty);
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
