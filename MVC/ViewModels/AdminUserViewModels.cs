using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using ServiceLayer.DTOs;

namespace MVC.ViewModels;

public sealed class AdminUsersIndexViewModel
{
    public CreateUserViewModel CreateUser { get; set; } = new();

    public ImportStudentsViewModel ImportStudents { get; set; } = new();

    public ResetAccountPasswordViewModel ResetPassword { get; set; } = new();

    public StudentImportResultViewModel? ImportResult { get; set; }

    public IReadOnlyList<AdminUserListItemViewModel> Users { get; set; } = [];

    public int StudentCount => Users.Count(u => u.Role == UserRoles.Student);

    public int TeacherCount => Users.Count(u => u.Role == UserRoles.Teacher);

    public int AdminCount => Users.Count(u => u.Role == UserRoles.Admin);

    public int BlockedCount => Users.Count(u => u.IsBlocked);

    public Guid? CurrentAdminUserId { get; set; }

    public IEnumerable<SelectListItem> SubjectOptions { get; set; } = [];
}

public sealed class AdminUserListItemViewModel
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? StudentCode { get; set; }

    public string Role { get; set; } = string.Empty;

    public bool IsBlocked { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? AssignedSubjectCode { get; set; }

    public string? AssignedSubjectName { get; set; }

    public bool IsHeadOfDepartment { get; set; }
}

public sealed class CreateUserViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(255, ErrorMessage = "Full name cannot exceed 255 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = UserRoles.Student;

    [StringLength(255, ErrorMessage = "Department cannot exceed 255 characters.")]
    public string? Department { get; set; }

    public Guid? SubjectId { get; set; }

    public bool IsHeadOfDepartment { get; set; }

    public IEnumerable<SelectListItem> RoleOptions { get; set; } =
    [
        new("Student", UserRoles.Student),
        new("Teacher", UserRoles.Teacher)
    ];
}

public sealed class ImportStudentsViewModel
{
    [Required(ErrorMessage = "Upload a Google Sheets export file.")]
    public IFormFile? File { get; set; }
}

public sealed class ResetAccountPasswordViewModel
{
    [Required(ErrorMessage = "Account email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid account email address.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = string.Empty;
}

public sealed class StudentImportResultViewModel
{
    public int TotalRows { get; set; }

    public int CreatedCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }

    public IReadOnlyList<StudentImportRowResultViewModel> Rows { get; set; } = [];
}

public sealed class StudentImportRowResultViewModel
{
    public int RowNumber { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
