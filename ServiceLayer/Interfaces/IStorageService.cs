namespace ServiceLayer.Interfaces;

/// <summary>
/// Abstraction over file storage so the rest of the app does not care whether files
/// live on local disk, Azure Blob, S3, etc. Task 3 ships a local-disk implementation.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Persists <paramref name="content"/> under a folder named after <paramref name="documentId"/>
    /// and returns a stable, forward-slash relative path (suitable for the Document.FileUrl column).
    /// </summary>
    Task<string> SaveAsync(
        Guid documentId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a previously stored file for reading. Throws FileNotFoundException if missing.</summary>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Returns true if a file exists at the given relative path.</summary>
    bool Exists(string relativePath);
}
