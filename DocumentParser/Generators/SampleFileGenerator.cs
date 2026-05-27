namespace DocumentParser.Generators;

/// <summary>
/// Thin coordinator — delegates to <see cref="PdfSampleGenerator"/> and
/// <see cref="DocxSampleGenerator"/> which each use only one library's namespace
/// to avoid CS0104 ambiguous-reference errors.
/// </summary>
public static class SampleFileGenerator
{
    public static void GenerateAll(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        string pdf1 = Path.Combine(outputDir, "sample1_text.pdf");
        string pdf2 = Path.Combine(outputDir, "sample2_multipage.pdf");
        string pdf3 = Path.Combine(outputDir, "sample3_scanned_sim.pdf");
        string docx = Path.Combine(outputDir, "sample.docx");

        PdfSampleGenerator.GenerateTextPdf(pdf1);
        Console.WriteLine($"  [PDF] Created: {Path.GetFileName(pdf1)}");

        PdfSampleGenerator.GenerateMultiPagePdf(pdf2);
        Console.WriteLine($"  [PDF] Created: {Path.GetFileName(pdf2)}");

        PdfSampleGenerator.GenerateScannedSimPdf(pdf3);
        Console.WriteLine($"  [PDF] Created: {Path.GetFileName(pdf3)}");

        DocxSampleGenerator.Generate(docx);
        Console.WriteLine($"  [DOCX] Created: {Path.GetFileName(docx)}");

        Console.WriteLine($"  → All sample files in: {outputDir}");
    }
}
