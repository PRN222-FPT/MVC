using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC.ViewModels;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace MVC.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public sealed class AdminUsersController : Controller
{
    private const long MaxStudentImportFileSizeBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedStudentImportExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".xlsx"
    };

    private readonly IUserManagementService _userManagementService;

    public AdminUsersController(IUserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await BuildIndexViewModelAsync(
            new CreateUserViewModel(),
            new ResetAccountPasswordViewModel(),
            new AssignTeacherSubjectViewModel(),
            cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "CreateUser")] CreateUserViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexViewModelAsync(
                viewModel, new ResetAccountPasswordViewModel(), new AssignTeacherSubjectViewModel(), cancellationToken));
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
            return View("Index", await BuildIndexViewModelAsync(
                viewModel, new ResetAccountPasswordViewModel(), new AssignTeacherSubjectViewModel(), cancellationToken));
        }

        TempData["Success"] = "Account created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignSubject(
        [Bind(Prefix = "AssignTeacherSubject")] AssignTeacherSubjectViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || viewModel.UserId is null || viewModel.SubjectId is null)
        {
            return View("Index", await BuildIndexViewModelAsync(
                new CreateUserViewModel(), new ResetAccountPasswordViewModel(), viewModel, cancellationToken));
        }

        AssignTeacherSubjectResultDto result = await _userManagementService.AssignTeacherSubjectAsync(
            new AssignTeacherSubjectDto(viewModel.UserId.Value, viewModel.SubjectId.Value, viewModel.IsHeadOfDepartment),
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage ?? "Could not assign the subject.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Subject assigned.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportStudents(
        [Bind(Prefix = "ImportStudents")] ImportStudentsViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (viewModel.File is null || viewModel.File.Length == 0)
        {
            TempData["Error"] = "Upload a .csv or .xlsx file exported from Google Sheets.";
            return RedirectToAction(nameof(Index));
        }

        if (viewModel.File.Length > MaxStudentImportFileSizeBytes)
        {
            TempData["Error"] = "Student import file cannot exceed 5 MB.";
            return RedirectToAction(nameof(Index));
        }

        string extension = Path.GetExtension(viewModel.File.FileName);
        if (!AllowedStudentImportExtensions.Contains(extension))
        {
            TempData["Error"] = "Only .csv and .xlsx files are supported.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await using Stream stream = viewModel.File.OpenReadStream();
            StudentAccountImportResultDto result = await _userManagementService.ImportStudentAccountsAsync(
                viewModel.File.FileName,
                stream,
                cancellationToken);

            TempData["ImportResult"] = JsonSerializer.Serialize(MapImportResult(result));
            TempData[result.Succeeded ? "Success" : "Error"] =
                $"Student import completed: {result.CreatedCount} created, {result.SkippedCount} skipped, {result.FailedCount} failed.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        [Bind(Prefix = "ResetPassword")] ResetAccountPasswordViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexViewModelAsync(
                new CreateUserViewModel(), viewModel, new AssignTeacherSubjectViewModel(), cancellationToken));
        }

        ResetAccountPasswordResultDto result = await _userManagementService.ResetAccountPasswordAsync(
            viewModel.Email,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage ?? "Password could not be reset.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Password reset. A temporary password was sent by email.";
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
        ResetAccountPasswordViewModel resetPassword,
        AssignTeacherSubjectViewModel assignTeacherSubject,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminUserListItemDto> users = await _userManagementService.GetUsersAsync(cancellationToken);
        IReadOnlyList<SubjectListItemDto> subjects = await _userManagementService.GetAssignableSubjectsAsync(cancellationToken);

        return new AdminUsersIndexViewModel
        {
            CreateUser = createUser,
            ResetPassword = resetPassword,
            AssignTeacherSubject = assignTeacherSubject,
            ImportResult = ReadImportResult(),
            CurrentAdminUserId = TryGetCurrentUserId(),
            SubjectOptions = subjects.Select(subject => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                $"{subject.SubjectCode} - {subject.SubjectName}",
                subject.SubjectId.ToString())),
            TeacherOptions = users
                .Where(u => string.Equals(u.Role, UserRoles.Teacher, StringComparison.OrdinalIgnoreCase))
                .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                    $"{u.FullName} ({u.Email})",
                    u.UserId.ToString())),
            Users = users.Select(u => new AdminUserListItemViewModel
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                StudentCode = u.StudentCode,
                Role = u.Role,
                IsBlocked = u.IsBlocked,
                CreatedAt = u.CreatedAt,
                AssignedSubjectCode = u.AssignedSubjectCode,
                AssignedSubjectName = u.AssignedSubjectName,
                IsHeadOfDepartment = u.IsHeadOfDepartment
            }).ToList()
        };
    }

    private StudentImportResultViewModel? ReadImportResult()
    {
        if (TempData["ImportResult"] is not string json)
        {
            return null;
        }

        return JsonSerializer.Deserialize<StudentImportResultViewModel>(json);
    }

    private static StudentImportResultViewModel MapImportResult(StudentAccountImportResultDto result) =>
        new()
        {
            TotalRows = result.TotalRows,
            CreatedCount = result.CreatedCount,
            SkippedCount = result.SkippedCount,
            FailedCount = result.FailedCount,
            Rows = result.Rows.Select(row => new StudentImportRowResultViewModel
            {
                RowNumber = row.RowNumber,
                StudentCode = row.StudentCode,
                FullName = row.FullName,
                Email = row.Email,
                Status = row.Status,
                Message = row.Message
            }).ToList()
        };

    private Guid? TryGetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out Guid parsed) ? parsed : null;
    }
}
