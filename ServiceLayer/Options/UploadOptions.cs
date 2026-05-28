namespace ServiceLayer.Options;

/// <summary>
/// Bound from the "Upload" configuration section. Controls file-upload validation
/// and where uploaded files are stored on disk.
/// </summary>
public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    /// <summary>Maximum accepted file size in bytes. Default 20 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 20L * 1024 * 1024;

    /// <summary>
    /// Allowed file extensions (lower-case, leading dot). Populated from configuration.
    /// Left empty by default so config values replace rather than append to a seed array.
    /// </summary>
    public string[] AllowedExtensions { get; set; } = [];

    /// <summary>Root folder for stored uploads. Relative paths resolve against the app working directory.</summary>
    public string StorageRoot { get; set; } = "uploads";
}
