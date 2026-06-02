using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class UserManagementService : IUserManagementService
{
    private readonly Prn222Context _context;
    private readonly IPasswordHashService _passwordHashService;

    public UserManagementService(Prn222Context context, IPasswordHashService passwordHashService)
    {
        _context = context;
        _passwordHashService = passwordHashService;
    }

    public async Task<IReadOnlyList<AdminUserListItemDto>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Email)
            .Select(u => new AdminUserListItemDto(
                u.UserId,
                u.FullName,
                u.Email,
                u.Role ?? UserRoles.Student,
                u.IsBlocked,
                u.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateManagedUserResultDto> CreateUserAsync(
        CreateManagedUserDto request,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = request.Email.Trim().ToLowerInvariant();
        string normalizedRole = request.Role.Trim().ToLowerInvariant();

        if (!UserRoles.IsAssignable(normalizedRole))
        {
            return new CreateManagedUserResultDto(false, "Only student and teacher accounts can be created here.");
        }

        bool emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            return new CreateManagedUserResultDto(false, "An account with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = _passwordHashService.HashPassword(request.Password),
            Role = normalizedRole
        };

        _context.Users.Add(user);

        if (normalizedRole == UserRoles.Teacher)
        {
            bool teacherExists = await _context.Teachers
                .AnyAsync(t => t.Email != null && t.Email.ToLower() == normalizedEmail, cancellationToken);

            if (!teacherExists)
            {
                _context.Teachers.Add(new Teacher
                {
                    FullName = user.FullName,
                    Email = normalizedEmail,
                    Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim()
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return new CreateManagedUserResultDto(true, null);
    }

    public async Task<BlockManagedUserResultDto> BlockUserAsync(
        Guid userId,
        Guid currentAdminUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == currentAdminUserId)
        {
            return new BlockManagedUserResultDto(false, "You cannot block your own administrator account.");
        }

        User? user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            return new BlockManagedUserResultDto(false, "User was not found.");
        }

        if (string.Equals(user.Role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return new BlockManagedUserResultDto(false, "Administrator accounts cannot be blocked here.");
        }

        if (user.IsBlocked)
        {
            return new BlockManagedUserResultDto(true, null);
        }

        user.IsBlocked = true;
        await _context.SaveChangesAsync(cancellationToken);

        return new BlockManagedUserResultDto(true, null);
    }

    public async Task EnsureAdminUserAsync(AdminUserSeedDto seed, CancellationToken cancellationToken = default)
    {
        string normalizedEmail = seed.Email.Trim().ToLowerInvariant();

        User? user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null)
        {
            _context.Users.Add(new User
            {
                FullName = "System Administrator",
                Email = normalizedEmail,
                PasswordHash = seed.PasswordHash,
                Role = UserRoles.Admin
            });

            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        bool changed = false;

        if (!string.Equals(user.Role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            user.Role = UserRoles.Admin;
            changed = true;
        }

        if (!string.Equals(user.PasswordHash, seed.PasswordHash, StringComparison.Ordinal))
        {
            user.PasswordHash = seed.PasswordHash;
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
