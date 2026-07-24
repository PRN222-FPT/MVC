using System.ComponentModel.DataAnnotations;
using ServiceLayer.Services;

namespace MVC.ViewModels;

public sealed class ChunkingSettingsIndexViewModel
{
    public UpdateChunkingSettingsViewModel UpdateChunkSize { get; set; } = new();

    public int CurrentChunkSizeCharacters { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByName { get; set; }
}

public sealed class UpdateChunkingSettingsViewModel
{
    [Required(ErrorMessage = "Chunk size is required.")]
    [Range(
        ChunkingSettingsService.MinChunkSizeCharacters,
        ChunkingSettingsService.MaxChunkSizeCharacters,
        ErrorMessage = "Chunk size must be between 100 and 8000 characters.")]
    public int ChunkSizeCharacters { get; set; }
}
