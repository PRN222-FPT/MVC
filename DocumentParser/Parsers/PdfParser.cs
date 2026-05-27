using DocumentParser.Models;
using DocumentParser.Ocr;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace DocumentParser.Parsers;

/// <summary>
/// Extracts text from PDF files using iText7, page by page, with an optional OCR fallback.
///
/// Strategy per page:
///   1. Try the embedded text layer via iText7 (fast, exact).
///   2. If the page yields no text AND an <see cref="IOcrEngine"/> was supplied,
///      rasterize the page to an image and run OCR (Tesseract) as a fallback.
///
/// LIMITATIONS (logged as warnings in ParseResult):
///   1. Scanned PDFs (image-only): no text layer — handled only when OCR is enabled.
///   2. Password-protected PDFs: throws PdfException unless password is supplied.
///   3. Text order: iText7 reads glyphs in PDF stream order, which may differ from visual reading
///      order in multi-column or RTL layouts.
///   4. Ligatures / special fonts: some Type3 or CID fonts map glyphs to private-use codepoints —
///      extracted text may contain garbled characters.
///   5. Form fields (AcroForm) are not included; use PdfAcroForm.GetField() separately if needed.
///   6. OCR accuracy depends on image resolution, language data, and scan quality — treat OCR
///      text as lower-confidence (ParsedPage.Source == TextSource.Ocr).
///   7. AGPL license: iText7 Community is AGPL-3.0 — commercial use requires a paid license.
/// </summary>
public sealed class PdfParser
{
    private readonly IOcrEngine? _ocrEngine;
    private readonly int _ocrDpi;

    /// <param name="ocrEngine">Optional OCR engine. When null, empty pages stay empty (no OCR).</param>
    /// <param name="ocrDpi">Rasterization DPI for OCR fallback. 300 is a good default.</param>
    public PdfParser(IOcrEngine? ocrEngine = null, int ocrDpi = 300)
    {
        _ocrEngine = ocrEngine;
        _ocrDpi = ocrDpi;
    }

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

        // Read bytes once: shared by iText7 (text layer) and the rasterizer (OCR fallback).
        byte[] pdfBytes = File.ReadAllBytes(filePath);

        using var reader = new PdfReader(new MemoryStream(pdfBytes));
        using var pdfDoc = new PdfDocument(reader);

        int totalPages = pdfDoc.GetNumberOfPages();
        int ocrPageCount = 0;
        int ocrFailedCount = 0;

        for (int pageNum = 1; pageNum <= totalPages; pageNum++)
        {
            var strategy = new SimpleTextExtractionStrategy();
            string rawText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(pageNum), strategy);
            string normalizedText = NormalizeText(rawText);

            if (!string.IsNullOrWhiteSpace(normalizedText))
            {
                // Text layer hit — fastest, most accurate path.
                pages.Add(new ParsedPage(pageNum, normalizedText, IsEmpty: false, TextSource.TextLayer));
                continue;
            }

            // Text layer empty. Try OCR fallback if enabled.
            if (_ocrEngine is null)
            {
                pages.Add(new ParsedPage(pageNum, string.Empty, IsEmpty: true, TextSource.None));
                continue;
            }

            string ocrText = TryOcrPage(pdfBytes, pageNum, warnings);
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                ocrPageCount++;
                pages.Add(new ParsedPage(pageNum, ocrText, IsEmpty: false, TextSource.Ocr));
            }
            else
            {
                ocrFailedCount++;
                pages.Add(new ParsedPage(pageNum, string.Empty, IsEmpty: true, TextSource.None));
            }
        }

        BuildWarnings(warnings, totalPages, pages, ocrPageCount, ocrFailedCount);

        return new ParseResult
        {
            SourceFile = Path.GetFileName(filePath),
            Format = DetermineFormat(pages),
            Pages = pages,
            Warnings = warnings
        };
    }

    // -------------------------------------------------------------------------
    // OCR fallback
    // -------------------------------------------------------------------------

    private string TryOcrPage(byte[] pdfBytes, int pageNum, List<string> warnings)
    {
        try
        {
            byte[] png = PdfPageRasterizer.RenderPageToPng(pdfBytes, pageNum - 1, _ocrDpi);
            return NormalizeText(_ocrEngine!.Recognize(png));
        }
        catch (Exception ex)
        {
            warnings.Add($"OCR failed on page {pageNum}: {ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }

    // -------------------------------------------------------------------------
    // Warning + format helpers
    // -------------------------------------------------------------------------

    private void BuildWarnings(
        List<string> warnings, int totalPages, List<ParsedPage> pages, int ocrPageCount, int ocrFailedCount)
    {
        int emptyCount = pages.Count(p => p.IsEmpty);

        if (_ocrEngine is null)
        {
            if (emptyCount == totalPages)
                warnings.Add(
                    "No text on any page and OCR is disabled. This PDF is likely image-based (scanned). " +
                    $"Enable OCR (Tesseract, '{nameof(IOcrEngine)}') to extract text from images.");
            else if (emptyCount > 0)
                warnings.Add(
                    $"{emptyCount}/{totalPages} pages returned no text (OCR disabled). " +
                    "These pages may contain only images or unsupported embedded fonts.");
        }
        else
        {
            if (ocrPageCount > 0)
                warnings.Add(
                    $"{ocrPageCount}/{totalPages} page(s) recovered via OCR ({_ocrEngine.Languages}). " +
                    "OCR text is lower-confidence — verify before downstream use.");
            if (ocrFailedCount > 0)
                warnings.Add(
                    $"{ocrFailedCount}/{totalPages} page(s) still empty after OCR " +
                    "(blank image, too low-res, or unsupported content).");
        }
    }

    private string DetermineFormat(List<ParsedPage> pages)
    {
        bool anyOcr = pages.Any(p => p.Source == TextSource.Ocr);
        bool anyEmpty = pages.Any(p => p.IsEmpty);

        if (anyOcr) return "PDF-ocr";
        if (anyEmpty && _ocrEngine is null) return "PDF-scanned";
        return "PDF-text";
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
