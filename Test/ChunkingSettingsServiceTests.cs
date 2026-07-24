using System;
using ServiceLayer.Services;
using Xunit;

namespace Test;

/// <summary>
/// Covers only the pure validation guard clause. Persistence against
/// <c>chunking_settings</c> is not unit-tested here: Prn222Context's model requires
/// Npgsql's UseVector() mapping for the Chunk.Embedding column (see Program.cs), which
/// isn't available under EF Core's InMemory provider, and no such test harness exists
/// elsewhere in this repo for Prn222Context-backed services (e.g. SubjectService,
/// DocumentService, UserManagementService are likewise untested at this layer).
/// Persistence is covered by manual verification (see the plan's Verification section).
/// </summary>
public class ChunkingSettingsServiceTests
{
    [Theory]
    [InlineData(99)]
    [InlineData(8001)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateChunkSizeAsync_OutOfRange_ReturnsFailureWithoutTouchingDatabase(int chunkSize)
    {
        // Passing a null context is safe here: the bounds guard clause returns before
        // the context is ever dereferenced.
        var service = new ChunkingSettingsService(null!);

        var result = await service.UpdateChunkSizeAsync(chunkSize, Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Contains("100", result.ErrorMessage);
        Assert.Contains("8000", result.ErrorMessage);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1400)]
    [InlineData(8000)]
    public void ChunkSizeBounds_MatchDocumentedRange(int chunkSize)
    {
        Assert.InRange(
            chunkSize,
            ChunkingSettingsService.MinChunkSizeCharacters,
            ChunkingSettingsService.MaxChunkSizeCharacters);
    }
}
