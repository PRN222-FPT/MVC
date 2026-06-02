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

public sealed record AdminUserListItemDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    bool IsBlocked,
    DateTime? CreatedAt);

public sealed record CreateManagedUserDto(
    string FullName,
    string Email,
    string Password,
    string Role,
    string? Department);

public sealed record CreateManagedUserResultDto(
    bool Succeeded,
    string? ErrorMessage);

public sealed record BlockManagedUserResultDto(
    bool Succeeded,
    string? ErrorMessage);

public sealed record AdminUserSeedDto(
    string Email,
    string PasswordHash);
