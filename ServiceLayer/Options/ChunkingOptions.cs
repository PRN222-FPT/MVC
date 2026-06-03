namespace ServiceLayer.Options;

/// <summary>
/// Options for configuring recursive character chunking.
/// </summary>
public sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    /// <summary>
    /// The maximum length of a text chunk in characters.
    /// Recommended default is 1400.
    /// </summary>
    public int ChunkSize { get; set; } = 1400;

    /// <summary>
    /// The number of characters that overlap between adjacent chunks.
    /// Recommended default is 180.
    /// </summary>
    public int ChunkOverlap { get; set; } = 180;

    /// <summary>
    /// The ordered list of separators to split on, from highest priority to lowest.
    /// Default: ["\r\n", "\n\n", "\n", " ", ""]
    /// </summary>
    public string[] Separators { get; set; } = ["\r\n", "\n\n", "\n", " ", ""];
}
