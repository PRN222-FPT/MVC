namespace ServiceLayer.DTOs;

/// <summary>
/// The admin-configured fixed chunk size (in characters) applied to future document uploads.
/// </summary>
public sealed record ChunkingSettingsDto(
    int ChunkSizeCharacters,
    DateTime? UpdatedAt,
    string? UpdatedByName
);

public sealed record UpdateChunkingSettingsResultDto(bool Succeeded, string? ErrorMessage);
