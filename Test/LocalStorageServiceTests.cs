using Microsoft.Extensions.Options;
using ServiceLayer.Options;
using ServiceLayer.Services;
using Xunit;

namespace Test;

public class LocalStorageServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"storage-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_RelativeStorageRoot_SavesUnderContentRoot()
    {
        var options = Options.Create(new UploadOptions
        {
            StorageRoot = "uploads"
        });
        var service = new LocalStorageService(options, _tempRoot);
        Guid documentId = Guid.NewGuid();
        await using var content = new MemoryStream("hello docx"u8.ToArray());

        string relativePath = await service.SaveAsync(documentId, content, @"C:\fake\sample.docx");

        string expectedFullPath = Path.Combine(_tempRoot, "uploads", documentId.ToString(), "sample.docx");
        Assert.Equal($"uploads/{documentId}/sample.docx", relativePath);
        Assert.True(File.Exists(expectedFullPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
