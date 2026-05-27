namespace DocumentParser.Models;

/// <summary>
/// Represents a single page's extracted content from a parsed document.
/// </summary>
public sealed record ParsedPage(
    int PageNumber,
    string Text,
    bool IsEmpty
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
