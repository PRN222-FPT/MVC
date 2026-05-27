using Microsoft.Extensions.Options;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;

namespace ServiceLayer.Services;

/// <summary>
/// Stores uploaded files on the local file system under <c>{StorageRoot}/{documentId}/{fileName}</c>.
/// Relative paths use forward slashes so they are stable across operating systems.
/// </summary>
public sealed class LocalStorageService : IStorageService
{
    private readonly UploadOptions _options;

    public LocalStorageService(IOptions<UploadOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SaveAsync(
        Guid documentId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Strip any directory components from the client file name (path-traversal guard).
        string safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = documentId.ToString();

        string folder = Path.Combine(GetAbsoluteRoot(), documentId.ToString());
        Directory.CreateDirectory(folder);

        string fullPath = Path.Combine(folder, safeFileName);
        await using (var fileStream = new FileStream(
            fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (content.CanSeek)
                content.Position = 0;
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return $"{_options.StorageRoot}/{documentId}/{safeFileName}".Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        string fullPath = ResolveFullPath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Stored file not found.", relativePath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public bool Exists(string relativePath) => File.Exists(ResolveFullPath(relativePath));

    // -------------------------------------------------------------------------

    private string GetAbsoluteRoot() =>
        Path.IsPathRooted(_options.StorageRoot)
            ? _options.StorageRoot
            : Path.Combine(Directory.GetCurrentDirectory(), _options.StorageRoot);

    private string ResolveFullPath(string relativePath)
    {
        // relativePath is "{StorageRoot}/{documentId}/{fileName}"; map it back to disk.
        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            return normalized;
        return Path.Combine(Directory.GetCurrentDirectory(), normalized);
    }
}
