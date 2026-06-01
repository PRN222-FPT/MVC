namespace ServiceLayer.Options;

/// <summary>
/// Options for configuring the Qdrant vector database client.
/// </summary>
public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    /// <summary>
    /// Host name or IP address of the Qdrant server.
    /// Default is "localhost".
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Port of the Qdrant gRPC endpoint.
    /// Default is 6334.
    /// </summary>
    public int Port { get; set; } = 6334;

    /// <summary>
    /// True to connect using HTTPS/TLS, false for unencrypted gRPC.
    /// Default is false.
    /// </summary>
    public bool Https { get; set; } = false;

    /// <summary>
    /// Optional API key for authenticating with a secured Qdrant instance.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Dimension of the vectors in the collection.
    /// Default is 1536 (OpenAI standard).
    /// </summary>
    public int VectorSize { get; set; } = 1536;
}
