using DataAccessLayer.Models;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class UserManagementService : IUserManagementService
{
    private readonly Prn222Context _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHashService _passwordHashService;

    public UserManagementService(
        Prn222Context context,
        IUnitOfWork unitOfWork,
        IPasswordHashService passwordHashService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHashService = passwordHashService;
    }

    public async Task<IReadOnlyList<AdminUserListItemDto>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Email)
            .ToListAsync(cancellationToken);

        string[] teacherEmails = users
            .Where(user => string.Equals(user.Role, UserRoles.Teacher, StringComparison.OrdinalIgnoreCase))
            .Select(user => user.Email.ToLowerInvariant())
            .ToArray();

        var teacherAssignments = await _context.Teachers
            .AsNoTracking()
            .Where(teacher => teacher.Email != null && teacherEmails.Contains(teacher.Email.ToLower()))
            .SelectMany(
                teacher => teacher.TeacherSubjects.Select(assignment => new
                {
                    Email = teacher.Email!,
                    assignment.Subject.SubjectCode,
                    assignment.Subject.SubjectName,
                    assignment.IsHeadOfDepartment
                }))
            .ToListAsync(cancellationToken);

        var assignmentByEmail = teacherAssignments
            .GroupBy(assignment => assignment.Email.ToLowerInvariant())
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(assignment => assignment.IsHeadOfDepartment)
                    .ThenBy(assignment => assignment.SubjectCode)
                    .First());

        return users
            .Select(user =>
            {
                assignmentByEmail.TryGetValue(user.Email.ToLowerInvariant(), out var assignment);
                return new AdminUserListItemDto(
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.Role ?? UserRoles.Student,
                    user.IsBlocked,
                    user.CreatedAt,
                    assignment?.SubjectCode,
                    assignment?.SubjectName,
                    assignment?.IsHeadOfDepartment ?? false);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SubjectListItemDto>> GetAssignableSubjectsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Subjects
            .AsNoTracking()
            .OrderBy(subject => subject.SubjectCode)
            .ThenBy(subject => subject.SubjectName)
            .Select(subject => new SubjectListItemDto(
                subject.SubjectId,
                subject.SubjectCode,
                subject.SubjectName,
                subject.Description,
                subject.CreatedAt))
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

        Subject? assignedSubject = null;
        if (normalizedRole == UserRoles.Teacher)
        {
            if (!request.SubjectId.HasValue || request.SubjectId.Value == Guid.Empty)
            {
                return new CreateManagedUserResultDto(false, "Teacher accounts must be assigned to a subject.");
            }

            assignedSubject = await _context.Subjects
                .FirstOrDefaultAsync(subject => subject.SubjectId == request.SubjectId.Value, cancellationToken);
            if (assignedSubject is null)
            {
                return new CreateManagedUserResultDto(false, "Selected subject was not found.");
            }
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
            Teacher? teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.Email != null && t.Email.ToLower() == normalizedEmail, cancellationToken);

            if (teacher is null)
            {
                teacher = new Teacher
                {
                    TeacherId = Guid.NewGuid(),
                    FullName = user.FullName,
                    Email = normalizedEmail,
                    Department = string.IsNullOrWhiteSpace(request.Department) ? null : request.Department.Trim()
                };
                _context.Teachers.Add(teacher);
            }

            bool assignmentExists = await _context.TeacherSubjects
                .AnyAsync(
                    assignment => assignment.TeacherId == teacher.TeacherId
                        && assignment.SubjectId == assignedSubject!.SubjectId,
                    cancellationToken);
            if (!assignmentExists)
            {
                _context.TeacherSubjects.Add(new TeacherSubject
                {
                    TeacherSubjectId = Guid.NewGuid(),
                    TeacherId = teacher.TeacherId,
                    SubjectId = assignedSubject!.SubjectId,
                    IsHeadOfDepartment = request.IsHeadOfDepartment
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

            await _unitOfWork.SaveChangesAsync(cancellationToken);
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
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
