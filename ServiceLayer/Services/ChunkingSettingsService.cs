using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class ChunkingSettingsService : IChunkingSettingsService
{
    public const int MinChunkSizeCharacters = 100;
    public const int MaxChunkSizeCharacters = 8000;

    private const short SettingsRowId = 1;

    private readonly Prn222Context _context;

    public ChunkingSettingsService(Prn222Context context)
    {
        _context = context;
    }

    public async Task<ChunkingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        ChunkingSetting settings = await GetSettingsRowAsync(asNoTracking: true, cancellationToken);

        return new ChunkingSettingsDto(
            settings.ChunkSizeCharacters,
            settings.UpdatedAt,
            settings.UpdatedByNavigation?.FullName);
    }

    public async Task<UpdateChunkingSettingsResultDto> UpdateChunkSizeAsync(
        int chunkSizeCharacters,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (chunkSizeCharacters < MinChunkSizeCharacters || chunkSizeCharacters > MaxChunkSizeCharacters)
        {
            return new UpdateChunkingSettingsResultDto(
                false,
                $"Chunk size must be between {MinChunkSizeCharacters} and {MaxChunkSizeCharacters} characters.");
        }

        ChunkingSetting settings = await GetSettingsRowAsync(asNoTracking: false, cancellationToken);

        settings.ChunkSizeCharacters = chunkSizeCharacters;
        settings.UpdatedBy = updatedByUserId;
        settings.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateChunkingSettingsResultDto(true, null);
    }

    private async Task<ChunkingSetting> GetSettingsRowAsync(bool asNoTracking, CancellationToken cancellationToken)
    {
        IQueryable<ChunkingSetting> query = _context.ChunkingSettings.Include(s => s.UpdatedByNavigation);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        ChunkingSetting? settings = await query.FirstOrDefaultAsync(s => s.Id == SettingsRowId, cancellationToken);
        if (settings is null)
        {
            throw new InvalidOperationException(
                "The chunking_settings row is missing. Run the chunking_settings migration script.");
        }

        return settings;
    }
}
