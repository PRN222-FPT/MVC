using DocumentParser.Generators;
using DocumentParser.Models;
using DocumentParser.Ocr;
using DocumentParser.Parsers;

// ─────────────────────────────────────────────────────────────────────────────
//  DocumentParser — PDF & DOCX parsing proof-of-concept
//  Libraries: iText7 9.1.0 (AGPL) | DocumentFormat.OpenXml 3.3.0 (MIT)
//             Tesseract 5.2.0 (Apache-2.0, OCR) | PDFtoImage (PDF→image)
// ─────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = System.Text.Encoding.UTF8;

string sampleDir = Path.Combine(AppContext.BaseDirectory, "SampleFiles");

// Build OCR engine once (null if tessdata is missing). Disposed at process exit.
using IOcrEngine? ocrEngine = BuildOcrEngine();

// ── Single-file mode ──────────────────────────────────────────────────────────
// Usage:  dotnet run -- "path/to/file.pdf"   (or .docx)
// Prints the FULL extracted text per page so you can inspect content directly.
if (args.Length > 0)
{
    ParseSingleFile(args[0], ocrEngine);
    return;
}

// ── Step 1: Generate sample fixtures ─────────────────────────────────────────
PrintSection("STEP 1 — Generating sample files");
SampleFileGenerator.GenerateAll(sampleDir);

// ── Step 2: Parse all PDFs (OCR fallback enabled if tessdata present) ─────────
PrintSection("STEP 2 — Parsing PDFs with iText7 (+ OCR fallback)");

var pdfParser = new PdfParser(ocrEngine);
var pdfFiles = new[]
{
    Path.Combine(sampleDir, "sample1_text.pdf"),
    Path.Combine(sampleDir, "sample2_multipage.pdf"),
    Path.Combine(sampleDir, "sample3_scanned_sim.pdf"),
};

foreach (var pdfPath in pdfFiles)
{
    ParseResult result = pdfParser.Parse(pdfPath);
    PrintParseResult(result);
}

// ── Step 3: Parse DOCX ────────────────────────────────────────────────────────
PrintSection("STEP 3 — Parsing DOCX with Open XML SDK");

var docxParser = new DocxParser();
ParseResult docxResult = docxParser.Parse(Path.Combine(sampleDir, "sample.docx"));
PrintParseResult(docxResult);

// ── Step 4: Limitation summary ───────────────────────────────────────────────
PrintSection("STEP 4 — Known Limitations");
PrintLimitationSummary();

Console.WriteLine();
Console.WriteLine("✅  All parsing demos completed successfully.");

// =============================================================================
// Single-file parsing (CLI arg mode)
// =============================================================================

static void ParseSingleFile(string path, IOcrEngine? ocrEngine)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"❌ File not found: {path}");
        return;
    }

    string ext = Path.GetExtension(path).ToLowerInvariant();
    PrintSection($"Parsing single file: {Path.GetFileName(path)}");

    ParseResult result;
    try
    {
        result = ext switch
        {
            ".pdf"  => new PdfParser(ocrEngine).Parse(path),
            ".docx" => new DocxParser().Parse(path),
            _ => throw new NotSupportedException(
                     $"Unsupported extension '{ext}'. Only .pdf and .docx are supported.")
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Parse failed: {ex.GetType().Name}: {ex.Message}");
        return;
    }

    // Header
    Console.WriteLine($"  File   : {result.SourceFile}");
    Console.WriteLine($"  Format : {result.Format}");
    Console.WriteLine($"  OCR    : {(ocrEngine is null ? "DISABLED (tessdata missing)" : $"ENABLED ({ocrEngine.Languages})")}");
    Console.WriteLine($"  Pages  : {result.TotalPages} total, {result.NonEmptyPages} with text");

    if (result.Warnings.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"  ⚠ Warnings:");
        foreach (var w in result.Warnings)
            Console.WriteLine($"    • {w}");
    }

    // FULL text per page (not just preview), tagged with extraction source.
    foreach (var page in result.Pages)
    {
        string tag = page.Source switch
        {
            TextSource.Ocr       => "  [via OCR]",
            TextSource.None      => "  [EMPTY — no extractable text]",
            _                    => "  [text layer]"
        };
        Console.WriteLine();
        Console.WriteLine(new string('─', 72));
        Console.WriteLine($"  PAGE {page.PageNumber}{tag}");
        Console.WriteLine(new string('─', 72));
        Console.WriteLine(page.IsEmpty ? "(no extractable text)" : page.Text);
    }

    Console.WriteLine();

    // Verdict for the OCR question.
    int ocrPages = result.Pages.Count(p => p.Source == TextSource.Ocr);
    if (result.NonEmptyPages == 0)
    {
        Console.WriteLine("════════════════════════════════════════════════════════════════════════");
        Console.WriteLine("  VERDICT: No text extracted from ANY page.");
        if (ocrEngine is null)
            Console.WriteLine("  OCR is DISABLED (tessdata folder missing). This file is image-based;");
        else
            Console.WriteLine("  OCR ran but found no readable text — image may be blank/too low-res.");
        Console.WriteLine("════════════════════════════════════════════════════════════════════════");
    }
    else if (ocrPages > 0)
    {
        Console.WriteLine("════════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  ✅ VERDICT: OCR WORKS. Recovered text from {ocrPages}/{result.TotalPages} image page(s) " +
                          $"using {ocrEngine!.Languages}.");
        Console.WriteLine("  Note: OCR text is lower-confidence than a real text layer.");
        Console.WriteLine("════════════════════════════════════════════════════════════════════════");
    }
    else
    {
        Console.WriteLine($"✅ Extracted text from {result.NonEmptyPages}/{result.TotalPages} pages " +
                          "via the text layer (OCR not needed for those pages).");
    }
}

// =============================================================================
// OCR engine factory
// =============================================================================

static IOcrEngine? BuildOcrEngine()
{
    string tessdata = Path.Combine(AppContext.BaseDirectory, "tessdata");
    try
    {
        var engine = new TesseractOcrEngine(tessdata, languages: "vie+eng");
        Console.WriteLine($"[OCR] Tesseract ready — languages: {engine.Languages}, tessdata: {tessdata}");
        return engine;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[OCR] Disabled — {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine("[OCR] PDFs without a text layer will return empty pages.");
        return null;
    }
}

// =============================================================================
// Console output helpers
// =============================================================================

static void PrintSection(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('═', 72));
    Console.WriteLine($"  {title}");
    Console.WriteLine(new string('═', 72));
}

static void PrintParseResult(ParseResult result)
{
    Console.WriteLine();
    Console.WriteLine($"  ┌─ {result.SourceFile}  [{result.Format}]");
    Console.WriteLine($"  │  Total pages   : {result.TotalPages}");
    Console.WriteLine($"  │  Non-empty     : {result.NonEmptyPages}");

    if (result.Warnings.Count > 0)
    {
        Console.WriteLine($"  │  ⚠ Warnings ({result.Warnings.Count}):");
        foreach (var w in result.Warnings)
            Console.WriteLine($"  │    • {w}");
    }

    Console.WriteLine($"  │");

    foreach (var page in result.Pages)
    {
        string status = page.IsEmpty ? "  [EMPTY — no extractable text]" : string.Empty;
        Console.WriteLine($"  │  Page {page.PageNumber,2}: {page.Preview}{status}");
    }

    Console.WriteLine($"  └─ end of {result.SourceFile}");
}

static void PrintLimitationSummary()
{
    Console.WriteLine();
    Console.WriteLine("  PDF — iText7 Community (AGPL-3.0)");
    Console.WriteLine("  ─────────────────────────────────────────────────────────────────");
    Console.WriteLine("  • Scanned PDFs (image-only): NO text extraction — OCR required (Tesseract).");
    Console.WriteLine("  • Password-protected PDFs: PdfException unless password supplied.");
    Console.WriteLine("  • Reading order: stream order ≠ visual order in multi-column / RTL layouts.");
    Console.WriteLine("  • Embedded fonts (Type3, CID): may produce garbled/missing characters.");
    Console.WriteLine("  • AcroForm fields, text overlays on images: NOT included.");
    Console.WriteLine("  • License: AGPL-3.0 — paid iText license required for closed-source apps.");
    Console.WriteLine();
    Console.WriteLine("  DOCX — DocumentFormat.OpenXml (MIT)");
    Console.WriteLine("  ─────────────────────────────────────────────────────────────────");
    Console.WriteLine("  • No true page numbering: SDK is a structural reader, not a renderer.");
    Console.WriteLine("    Page count = explicit <w:pageBreak> elements only.");
    Console.WriteLine("  • Text boxes, drawing canvas: NOT extracted from main flow.");
    Console.WriteLine("  • Headers / footers / footnotes / endnotes: NOT included.");
    Console.WriteLine("  • .doc (Word 97-2003): NOT supported — DOCX/DOCM/DOTX only.");
    Console.WriteLine("  • Password-encrypted DOCX: InvalidDataException at open.");
    Console.WriteLine("  • Table structure: flattened to text rows — visual layout lost.");
}
