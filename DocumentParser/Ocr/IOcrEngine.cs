namespace DocumentParser.Ocr;

/// <summary>
/// Abstraction over an OCR engine so the parser does not depend on a concrete library.
/// Swap Tesseract for another engine without touching PdfParser.
/// </summary>
public interface IOcrEngine : IDisposable
{
    /// <summary>Languages the engine was initialized with (e.g. "vie+eng").</summary>
    string Languages { get; }

    /// <summary>Runs OCR over a raster image (PNG/JPEG/TIFF/BMP bytes) and returns recognized text.</summary>
    string Recognize(byte[] imageBytes);
}
