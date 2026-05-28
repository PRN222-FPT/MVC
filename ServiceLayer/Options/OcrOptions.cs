namespace ServiceLayer.Options;

/// <summary>
/// Options for configuring OCR fallback during PDF parsing.
/// </summary>
public sealed class OcrOptions
{
    public const string SectionName = "Ocr";

    /// <summary>
    /// Whether OCR is enabled as a fallback for scanned pages.
    /// Enabled by default.
    /// </summary>
    public bool EnableOcr { get; set; } = true;

    /// <summary>
    /// Languages used for Tesseract OCR.
    /// Default: "vie+eng"
    /// </summary>
    public string Languages { get; set; } = "vie+eng";

    /// <summary>
    /// Rasterization resolution in DPI.
    /// Default: 300
    /// </summary>
    public int Dpi { get; set; } = 300;

    /// <summary>
    /// Path to the folder containing tessdata language files.
    /// If null, falls back to the executing directory.
    /// </summary>
    public string? TessdataPath { get; set; }
}
