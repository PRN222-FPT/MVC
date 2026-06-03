using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using ServiceLayer.DTOs;

namespace MVC.ViewModels;

public sealed class AdminUsersIndexViewModel
{
    public CreateUserViewModel CreateUser { get; set; } = new();

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
