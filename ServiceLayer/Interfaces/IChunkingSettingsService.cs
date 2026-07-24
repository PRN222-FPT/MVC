using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

/// <summary>
/// Manages the admin-configured fixed chunk size (in characters) applied when
/// processing documents that teachers upload.
/// </summary>
public interface IChunkingSettingsService
{
    Task<ChunkingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<UpdateChunkingSettingsResultDto> UpdateChunkSizeAsync(
        int chunkSizeCharacters,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default);
}
