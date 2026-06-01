namespace ServiceLayer.Interfaces;

/// <summary>
/// Generates dense vector embeddings for a batch of text inputs.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Returns one embedding vector per input text, in the same order.
    /// </summary>
public interface IEmbeddingService
{
    Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}
