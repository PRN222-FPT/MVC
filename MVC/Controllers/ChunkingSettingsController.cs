using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace MVC.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public sealed class ChunkingSettingsController : Controller
{
    private readonly IChunkingSettingsService _chunkingSettingsService;

    public ChunkingSettingsController(IChunkingSettingsService chunkingSettingsService)
    {
        _chunkingSettingsService = chunkingSettingsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildIndexViewModelAsync(new UpdateChunkingSettingsViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        [Bind(Prefix = "UpdateChunkSize")] UpdateChunkingSettingsViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexViewModelAsync(viewModel, cancellationToken));
        }

        Guid? currentAdminUserId = TryGetCurrentUserId();
        if (currentAdminUserId is null)
        {
            return Unauthorized();
        }

        UpdateChunkingSettingsResultDto result = await _chunkingSettingsService.UpdateChunkSizeAsync(
            viewModel.ChunkSizeCharacters,
            currentAdminUserId.Value,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update the chunk size.");
            return View("Index", await BuildIndexViewModelAsync(viewModel, cancellationToken));
        }

        TempData["Success"] = "Chunk size updated. It will apply to documents uploaded from now on.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ChunkingSettingsIndexViewModel> BuildIndexViewModelAsync(
        UpdateChunkingSettingsViewModel updateChunkSize,
        CancellationToken cancellationToken)
    {
        ChunkingSettingsDto settings = await _chunkingSettingsService.GetSettingsAsync(cancellationToken);

        if (updateChunkSize.ChunkSizeCharacters == 0)
        {
            updateChunkSize.ChunkSizeCharacters = settings.ChunkSizeCharacters;
        }

        return new ChunkingSettingsIndexViewModel
        {
            UpdateChunkSize = updateChunkSize,
            CurrentChunkSizeCharacters = settings.ChunkSizeCharacters,
            UpdatedAt = settings.UpdatedAt,
            UpdatedByName = settings.UpdatedByName
        };
    }

    private Guid? TryGetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out Guid parsed) ? parsed : null;
    }
}
