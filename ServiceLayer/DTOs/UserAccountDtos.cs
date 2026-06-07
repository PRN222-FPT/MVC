namespace ServiceLayer.DTOs;

public static class UserRoles
{
    public const string Admin = "admin";
    public const string Student = "student";
    public const string Teacher = "teacher";
    public const string DisabledLegacyRoute = "__disabled_legacy_route";

    public static readonly string[] AssignableRoles = [Student, Teacher];

    public static bool IsAssignable(string role) =>
        AssignableRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

public sealed record AuthenticatedUserDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role);

public sealed record LoginRequestDto(
    string Email,
    string Password);

public sealed record ChangePasswordRequestDto(
    Guid UserId,
    string CurrentPassword,
    string NewPassword);

public sealed record ChangePasswordResultDto(
    bool Succeeded,
    string? ErrorMessage);

public sealed record AdminUserListItemDto(
    Guid UserId,
    string FullName,
    string Email,
    string? StudentCode,
    string Role,
    bool IsBlocked,
    DateTime? CreatedAt,
    string? AssignedSubjectCode = null,
    string? AssignedSubjectName = null,
    bool IsHeadOfDepartment = false);

public sealed record CreateManagedUserDto(
    string FullName,
    string Email,
    string Password,
    string Role,
    string? Department,
    Guid? SubjectId = null,
    bool IsHeadOfDepartment = false);

public sealed record CreateManagedUserResultDto(
    bool Succeeded,
    string? ErrorMessage);

public sealed record BlockManagedUserResultDto(
    bool Succeeded,
    string? ErrorMessage);

public sealed record ResetAccountPasswordResultDto(
    bool Succeeded,
    string? ErrorMessage);

public sealed record AdminUserSeedDto(
    string Email,
    string Password);

public sealed record StudentImportRowDto(
    int RowNumber,
    string StudentCode,
    string FullName,
    string Email);

public sealed record StudentAccountImportRowResultDto(
    int RowNumber,
    string StudentCode,
    string FullName,
    string Email,
    string Status,
    string Message);

public sealed record StudentAccountImportResultDto(
    int TotalRows,
    int CreatedCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<StudentAccountImportRowResultDto> Rows)
{
    public bool Succeeded => FailedCount == 0;
}
