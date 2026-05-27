using DocumentParser.Generators;
using DocumentParser.Models;
using DocumentParser.Parsers;

// ─────────────────────────────────────────────────────────────────────────────
//  DocumentParser — PDF & DOCX parsing proof-of-concept
//  Libraries: iText7 9.1.0 (AGPL)  |  DocumentFormat.OpenXml 3.3.0 (MIT)
// ─────────────────────────────────────────────────────────────────────────────

Console.OutputEncoding = System.Text.Encoding.UTF8;

string sampleDir = Path.Combine(AppContext.BaseDirectory, "SampleFiles");

// ── Step 1: Generate sample fixtures ─────────────────────────────────────────
PrintSection("STEP 1 — Generating sample files");
SampleFileGenerator.GenerateAll(sampleDir);

// ── Step 2: Parse all PDFs ────────────────────────────────────────────────────
PrintSection("STEP 2 — Parsing PDFs with iText7");

var pdfParser = new PdfParser();
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
