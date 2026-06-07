using PDFtoImage;
using SkiaSharp;

namespace DocumentParser.Ocr;

/// <summary>
/// Renders a PDF page to a PNG raster using PDFtoImage (PDFium engine).
/// Needed because iText7 cannot rasterize pages — and Tesseract needs an image, not a PDF.
///
/// 300 DPI is the recommended sweet spot for OCR: high enough for accuracy,
/// low enough to keep memory/CPU reasonable. Raise to 400–600 for small fonts.
/// </summary>
public static class PdfPageRasterizer
{
    /// <param name="pdfBytes">Full PDF file bytes.</param>
    /// <param name="pageIndex">0-based page index (page 1 == index 0).</param>
    /// <param name="dpi">Render resolution. 300 is a good OCR default.</param>
    /// <returns>PNG-encoded image bytes for the requested page.</returns>
    public static byte[] RenderPageToPng(byte[] pdfBytes, int pageIndex, int dpi = 300)
    {
        // CA1416: PDFtoImage targets Windows/Linux/macOS via bundled PDFium native assets.
        // This tool runs on those desktop platforms, so the platform-guard warning is moot.
#pragma warning disable CA1416
        using SKBitmap bitmap = Conversion.ToImage(
            pdfBytes,
            password: null,
            page: pageIndex,
            options: new RenderOptions(Dpi: dpi));
#pragma warning restore CA1416

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        return data.ToArray();
    }
}
