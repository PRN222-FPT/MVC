using DocumentParser.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace DocumentParser.Parsers;

/// <summary>
/// Extracts text from PDF files using iText7, page by page.
///
/// LIMITATIONS (logged as warnings in ParseResult):
///   1. Scanned PDFs (image-only): iText7 extracts no text; OCR engine (e.g. Tesseract) required.
///   2. Password-protected PDFs: throws PdfException unless password is supplied.
///   3. Text order: iText7 reads glyphs in PDF stream order, which may differ from visual reading
///      order in multi-column or RTL layouts.
///   4. Ligatures / special fonts: some Type3 or CID fonts map glyphs to private-use codepoints —
///      extracted text may contain garbled characters.
///   5. Embedded images containing text are not extracted.
///   6. Form fields (AcroForm) are not included; use PdfAcroForm.GetField() separately if needed.
///   7. AGPL license: iText7 Community is AGPL-3.0 — commercial use requires a paid license.
/// </summary>
public sealed class PdfParser
{
    /// <summary>
    /// Parses every page of the PDF at <paramref name="filePath"/> and returns
    /// a <see cref="ParseResult"/> with one <see cref="ParsedPage"/> per page.
    /// </summary>
    public ParseResult Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        var warnings = new List<string>();
        var pages = new List<ParsedPage>();

        using var reader = new PdfReader(filePath);
        using var pdfDoc = new PdfDocument(reader);

        int totalPages = pdfDoc.GetNumberOfPages();

        // Heuristic: if first page yields no text, flag as potentially scanned.
        bool potentiallyScanned = false;

        for (int pageNum = 1; pageNum <= totalPages; pageNum++)
        {
            var strategy = new SimpleTextExtractionStrategy();
            string rawText = PdfTextExtractor.GetTextFromPage(
                pdfDoc.GetPage(pageNum),
                strategy
            );

            // Normalize whitespace while preserving paragraph structure.
            string normalizedText = NormalizeText(rawText);
            bool isEmpty = string.IsNullOrWhiteSpace(normalizedText);

            if (pageNum == 1 && isEmpty)
                potentiallyScanned = true;

            pages.Add(new ParsedPage(
                PageNumber: pageNum,
                Text: normalizedText,
                IsEmpty: isEmpty
            ));
        }

        if (potentiallyScanned)
            warnings.Add(
                "Page 1 yielded no text. The PDF may be image-based (scanned). " +
                "iText7 cannot extract text from scanned images — use an OCR library (e.g. Tesseract.NET)."
            );

        int emptyCount = pages.Count(p => p.IsEmpty);
        if (emptyCount > 0 && emptyCount < totalPages)
            warnings.Add(
                $"{emptyCount}/{totalPages} pages returned no text. " +
                "These pages may contain only images, graphics, or use unsupported embedded fonts."
            );

        // Determine format label.
        string format = potentiallyScanned ? "PDF-scanned" : "PDF-text";

        return new ParseResult
        {
            SourceFile = Path.GetFileName(filePath),
            Format = format,
            Pages = pages,
            Warnings = warnings
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string NormalizeText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        // Collapse runs of spaces/tabs; keep single newlines as paragraph breaks.
        var lines = raw.Split('\n');
        var normalized = lines
            .Select(line => string.Join(' ',
                line.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0);

        return string.Join("\n", normalized).Trim();
    }
}
