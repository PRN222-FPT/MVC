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
                viewModel.Department),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not create the account.");
            return View("Index", await BuildIndexViewModelAsync(viewModel, cancellationToken));
        }

        TempData["Success"] = "Account created.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminUsersIndexViewModel> BuildIndexViewModelAsync(
        CreateUserViewModel createUser,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminUserListItemDto> users = await _userManagementService.GetUsersAsync(cancellationToken);

        return new AdminUsersIndexViewModel
        {
            CreateUser = createUser,
            Users = users.Select(u => new AdminUserListItemViewModel
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                CreatedAt = u.CreatedAt
            }).ToList()
        };
    }
}
