using DocumentParser.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocumentParser.Parsers;

/// <summary>
/// Extracts text from DOCX files using Open XML SDK, grouped by logical "page".
///
/// LIMITATIONS (logged as warnings in ParseResult):
///   1. No true page-boundary support: DOCX is a flow document — actual page breaks depend
///      on the rendering engine (Word, LibreOffice). Open XML SDK does not paginate.
///      "Page number" here = count of explicit page-break elements (&lt;w:pageBreak/&gt; or
///      &lt;w:lastRenderedPageBreak/&gt;). Documents without explicit breaks are reported as 1 page.
///   2. Headers and footers: excluded from main text stream. Use GetHeaderFooterText() if needed.
///   3. Text boxes / drawing canvas: text inside &lt;mc:AlternateContent&gt; / &lt;w:txbxContent&gt;
///      is NOT extracted by the main paragraph walk.
///   4. Tables: cell text is included (row-by-row, cell-by-cell) but table structure is lost.
///   5. Footnotes and endnotes: stored in separate XML parts, not included here.
///   6. Revision marks: "deleted" runs (&lt;w:del&gt;) are skipped; "inserted" runs (&lt;w:ins&gt;) are included.
///   7. Password-encrypted DOCX: throws InvalidDataException — must be decrypted first.
///   8. Legacy .doc (Word 97-2003): not supported — Open XML SDK reads only .docx/.docm/.dotx.
/// </summary>
public sealed class DocxParser
{
    /// <summary>
    /// Parses the DOCX at <paramref name="filePath"/> and returns a <see cref="ParseResult"/>
    /// where each <see cref="ParsedPage"/> corresponds to an explicit page break in the document.
    /// If no explicit breaks exist, the entire document is returned as page 1.
    /// </summary>
    public ParseResult Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("DOCX file not found.", filePath);

        using var wordDoc = WordprocessingDocument.Open(filePath, isEditable: false);
        return ParseDocument(wordDoc, Path.GetFileName(filePath));
    }

    /// <summary>
    /// Parses the DOCX from a <see cref="Stream"/> and returns a <see cref="ParseResult"/>.
    /// </summary>
    public ParseResult Parse(Stream stream, string sourceFile = "unknown.docx")
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var wordDoc = WordprocessingDocument.Open(stream, isEditable: false);
        return ParseDocument(wordDoc, sourceFile);
    }

    private ParseResult ParseDocument(WordprocessingDocument wordDoc, string sourceFile)
    {
        var warnings = new List<string>();
        var pages = new List<ParsedPage>();

        var body = wordDoc.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("DOCX body is null — file may be corrupt.");

        // Walk paragraphs and tables; split on explicit page breaks.
        var currentPageLines = new List<string>();
        int pageNumber = 1;
        bool hasExplicitBreaks = false;

        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case Paragraph para:
                    ProcessParagraph(para, currentPageLines, ref pageNumber, pages, ref hasExplicitBreaks);
                    break;

                case Table table:
                    ProcessTable(table, currentPageLines);
                    break;

                // Ignore other block-level elements (sdt, bookmarkStart, etc.)
            }
        }

        // Flush last page.
        FlushPage(pageNumber, currentPageLines, pages);

        if (!hasExplicitBreaks)
            warnings.Add(
                "No explicit page-break elements found. The entire document is returned as page 1. " +
                "DOCX page numbering requires rendering — use Microsoft.Office.Interop.Word or " +
                "a paid library (Aspose.Words, Spire.Doc) for accurate page boundaries."
            );

        warnings.Add(
            "Text boxes, headers/footers, footnotes, and endnotes are NOT included in this extraction."
        );

        return new ParseResult
        {
            SourceFile = sourceFile,
            Format = "DOCX",
            Pages = pages,
            Warnings = warnings
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static void ProcessParagraph(
        Paragraph para,
        List<string> currentLines,
        ref int pageNumber,
        List<ParsedPage> pages,
        ref bool hasExplicitBreaks)
    {
        // Check for explicit page break before collecting text.
        bool hasPageBreak = para.Descendants<Break>()
            .Any(b => b.Type?.Value == BreakValues.Page)
            || para.Descendants<LastRenderedPageBreak>().Any();

        if (hasPageBreak)
        {
            hasExplicitBreaks = true;
            // Flush current page, then start new one.
            FlushPage(pageNumber, currentLines, pages);
            currentLines.Clear();
            pageNumber++;
        }

        // Collect run text, skip deleted runs.
        var runTexts = para
            .Descendants<Run>()
            .Where(run => run.Parent is not DeletedRun)
            .Select(run => run.InnerText)
            .Where(t => !string.IsNullOrEmpty(t));

        string paraText = string.Join(string.Empty, runTexts).Trim();

        if (!string.IsNullOrEmpty(paraText))
            currentLines.Add(paraText);
    }

    private static void ProcessTable(Table table, List<string> currentLines)
    {
        foreach (var row in table.Elements<TableRow>())
        {
            var cellTexts = row
                .Elements<TableCell>()
                .Select(cell => cell.InnerText.Trim())
                .Where(t => !string.IsNullOrEmpty(t));

            string rowLine = string.Join(" | ", cellTexts);
            if (!string.IsNullOrEmpty(rowLine))
                currentLines.Add(rowLine);
        }
    }

    private static void FlushPage(int pageNumber, List<string> lines, List<ParsedPage> pages)
    {
        string text = string.Join("\n", lines).Trim();
        pages.Add(new ParsedPage(
            PageNumber: pageNumber,
            Text: text,
            IsEmpty: string.IsNullOrWhiteSpace(text)
        ));
    }
}
