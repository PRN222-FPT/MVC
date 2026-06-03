using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace MVC.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public sealed class AdminUsersController : Controller
{
    private readonly IUserManagementService _userManagementService;

    public AdminUsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildIndexViewModelAsync(new CreateUserViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "CreateUser")] CreateUserViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexViewModelAsync(viewModel, cancellationToken));
        }

        var result = await _userManagementService.CreateUserAsync(
            new CreateManagedUserDto(
                viewModel.FullName,
                viewModel.Email,
                viewModel.Password,
                viewModel.Role,
                viewModel.Department,
                viewModel.SubjectId,
                viewModel.IsHeadOfDepartment),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not create the account.");
            return View("Index", await BuildIndexViewModelAsync(viewModel, cancellationToken));
        }

        TempData["Success"] = "Account created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(Guid userId, CancellationToken cancellationToken)
    {
        Guid? currentAdminUserId = TryGetCurrentUserId();
        if (currentAdminUserId is null)
        {
            return Unauthorized();
        }

        BlockManagedUserResultDto result = await _userManagementService.BlockUserAsync(
            userId,
            currentAdminUserId.Value,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage ?? "Could not block the account.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Account blocked.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminUsersIndexViewModel> BuildIndexViewModelAsync(
        CreateUserViewModel createUser,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminUserListItemDto> users = await _userManagementService.GetUsersAsync(cancellationToken);
        IReadOnlyList<SubjectListItemDto> subjects = await _userManagementService.GetAssignableSubjectsAsync(cancellationToken);

        return new AdminUsersIndexViewModel
        {
            CreateUser = createUser,
            CurrentAdminUserId = TryGetCurrentUserId(),
            SubjectOptions = subjects.Select(subject => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                $"{subject.SubjectCode} - {subject.SubjectName}",
                subject.SubjectId.ToString())),
            Users = users.Select(u => new AdminUserListItemViewModel
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                IsBlocked = u.IsBlocked,
                CreatedAt = u.CreatedAt,
                AssignedSubjectCode = u.AssignedSubjectCode,
                AssignedSubjectName = u.AssignedSubjectName,
                IsHeadOfDepartment = u.IsHeadOfDepartment
            }).ToList()
        };
    }

    private Guid? TryGetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out Guid parsed) ? parsed : null;
    }
}
