# DocumentParser — PDF & DOCX Parsing PoC

Proof-of-concept console app that validates **iText7** (PDF) and **DocumentFormat.OpenXml** (DOCX)
can be used as document parsing backends in the GROUP1_Ass1 project.

## Libraries

| Library | Version | License | Purpose |
|---------|---------|---------|---------|
| `itext7` | 9.1.0 | **AGPL-3.0** | Parse text-layer PDFs, track page numbers |
| `itext7.bouncy-castle-adapter` | 9.1.0 | AGPL-3.0 | Required cryptography backend for iText7 9.x |
| `DocumentFormat.OpenXml` | 3.3.0 | MIT | Parse DOCX files, extract structured text |

> ⚠️ **iText7 License**: Community edition is AGPL-3.0. Any closed-source application that
> distributes iText7 **must** purchase a commercial iText license or use an alternative
> (e.g. PdfPig — MIT, or Aspose.PDF — commercial).

## How to Run

```bash
# From the repo root (where GROUP1_Ass1.slnx lives):
cd DocumentParser
dotnet run -c Release

# Or from the solution root:
dotnet run --project DocumentParser/DocumentParser.csproj -c Release
```

## What It Does

1. **Generates 4 sample fixtures** into `bin/Release/net10.0/SampleFiles/`:
   - `sample1_text.pdf` — single-page, text-layer PDF
   - `sample2_multipage.pdf` — 3-page PDF with distinct per-page content
   - `sample3_scanned_sim.pdf` — page 1 is draw-only (no text stream), page 2 has text
   - `sample.docx` — 3-section DOCX with explicit page breaks and a table

2. **Parses all 3 PDFs** and logs each page's text with its page number.

3. **Parses the DOCX** and logs each logical page's text.

4. **Prints known limitations** for each format.

## Parsing with Your Own Files

```bash
# Drop your files into DocumentParser/SampleFiles/ and edit Program.cs
# to point to them, or modify the pdfFiles array:
var pdfFiles = new[]
{
    "path/to/your/file.pdf",
    ...
};
```

## Page Number Tracking

### PDF (iText7)
`PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(pageNum), strategy)` is called per page.
The loop index **is** the page number (1-based). No inference needed.

### DOCX (Open XML SDK)
The SDK has no renderer — it cannot compute visual page breaks.
Page counter increments on each `<w:pageBreak/>` element encountered.
Documents without explicit breaks are returned as **a single page**.

## Known Limitations

### PDF
| Issue | Impact | Workaround |
|-------|--------|------------|
| Scanned pages (image-only) | Empty string returned | Add Tesseract.NET for OCR |
| Multi-column / RTL layouts | Text order may be wrong | Use `LocationTextExtractionStrategy` |
| Type3 / CID embedded fonts | Garbled characters | Pre-process font mapping |
| AcroForm fields | Not extracted | Use `PdfAcroForm.GetAllFormFields()` |
| Password-protected PDF | `PdfException` at open | Supply password via `ReaderProperties` |
| **AGPL license** | Closed-source apps need paid license | PdfPig (MIT) or Aspose.PDF |

### DOCX
| Issue | Impact | Workaround |
|-------|--------|------------|
| No true page boundaries | Page = explicit `<w:pageBreak>` only | Use Aspose.Words / Spire.Doc |
| Text boxes & drawing canvas | Not extracted | Walk `mc:AlternateContent` / `w:txbxContent` |
| Headers / footers | Not extracted | Read `HeaderPart` / `FooterPart` separately |
| Footnotes / endnotes | Not extracted | Read `FootnotesPart` / `EndnotesPart` |
| `.doc` (Word 97-2003) | `InvalidDataException` | Convert to .docx first |
| Encrypted DOCX | `InvalidDataException` | Decrypt first (OfficeOpenXml) |

## Project Structure

```
DocumentParser/
├── Models/
│   └── ParsedPage.cs          ParsedPage record + ParseResult aggregate
├── Parsers/
│   ├── PdfParser.cs           iText7 — page-by-page text extraction
│   └── DocxParser.cs          Open XML SDK — paragraph/table walk + page break detection
├── Generators/
│   ├── PdfSampleGenerator.cs  Creates 3 sample PDFs (iText7 usings only)
│   ├── DocxSampleGenerator.cs Creates sample DOCX (Open XML SDK usings only)
│   └── SampleFileGenerator.cs Thin coordinator (no ambiguous usings)
└── Program.cs                 Entry point — generate → parse → log → summarize
```

> **Why two generator files?** Both iText7 and Open XML SDK define `Paragraph`, `Document`,
> `Table`, `Text`, `Path`, `PageSize`. Splitting into separate files with non-overlapping
> `using` directives is the cleanest way to avoid CS0104 ambiguous-reference errors.
