using Tesseract;

namespace DocumentParser.Ocr;

/// <summary>
/// Tesseract-backed OCR engine (Apache-2.0 — free for academic and commercial use).
///
/// Requires:
///   • Native Tesseract + Leptonica libraries (deployed by the `Tesseract` NuGet package).
///   • A `tessdata` folder containing the language files (e.g. eng.traineddata, vie.traineddata).
///
/// The underlying <see cref="TesseractEngine"/> is NOT thread-safe — create one instance per
/// thread, or serialize calls. This class is intended for single-threaded ingestion use.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly TesseractEngine _engine;

    public string Languages { get; }

    /// <param name="tessdataPath">Folder containing *.traineddata files.</param>
    /// <param name="languages">'+'-joined language codes, e.g. "vie+eng".</param>
    public TesseractOcrEngine(string tessdataPath, string languages = "vie+eng")
    {
        if (!Directory.Exists(tessdataPath))
            throw new DirectoryNotFoundException(
                $"tessdata folder not found: {tessdataPath}. " +
                "Ensure *.traineddata files are present and copied to the output directory.");

        // Verify each requested language file exists for a clearer error than Tesseract's native crash.
        foreach (var lang in languages.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            string file = Path.Combine(tessdataPath, $"{lang}.traineddata");
            if (!File.Exists(file))
                throw new FileNotFoundException(
                    $"Missing language file '{lang}.traineddata' in {tessdataPath}.", file);
        }

        Languages = languages;

        // EngineMode.Default uses the LSTM engine, compatible with tessdata_fast / tessdata_best.
        _engine = new TesseractEngine(tessdataPath, languages, EngineMode.Default);
    }

    public string Recognize(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            return string.Empty;

        using var pix = Pix.LoadFromMemory(imageBytes);
        using var page = _engine.Process(pix);
        return page.GetText() ?? string.Empty;
    }

    public void Dispose() => _engine.Dispose();
}
