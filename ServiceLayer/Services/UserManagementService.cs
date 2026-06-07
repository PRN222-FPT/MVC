using DataAccessLayer.Models;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using System.Net.Mail;
using System.Security.Cryptography;

namespace ServiceLayer.Services;

public sealed class UserManagementService : IUserManagementService
{
    private const int GeneratedPasswordLength = 14;

    private readonly Prn222Context _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IEmailSender _emailSender;

    public UserManagementService(
        Prn222Context context,
        IUnitOfWork unitOfWork,
        IPasswordHashService passwordHashService,
        IEmailSender emailSender)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _passwordHashService = passwordHashService;
        _emailSender = emailSender;
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
                    user.StudentCode,
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

    public async Task<StudentAccountImportResultDto> ImportStudentAccountsAsync(
        string fileName,
        Stream fileContent,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StudentImportRowDto> rows = await StudentRosterParser.ParseAsync(
            fileName,
            fileContent,
            cancellationToken);

        var results = new List<StudentAccountImportRowResultDto>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenStudentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (StudentImportRowDto row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string studentCode = row.StudentCode.Trim();
            string fullName = row.FullName.Trim();
            string normalizedEmail = row.Email.Trim().ToLowerInvariant();

            string? validationError = ValidateStudentImportRow(studentCode, fullName, normalizedEmail);
            if (validationError is not null)
            {
                results.Add(ToResult(row, "Failed", validationError));
                continue;
            }

            if (!seenEmails.Add(normalizedEmail))
            {
                results.Add(ToResult(row, "Skipped", "Duplicate email in uploaded file."));
                continue;
            }

            if (!seenStudentCodes.Add(studentCode))
            {
                results.Add(ToResult(row, "Skipped", "Duplicate MSSV in uploaded file."));
                continue;
            }

            bool existingAccount = await _context.Users
                .AnyAsync(user => user.Email.ToLower() == normalizedEmail, cancellationToken);
            if (existingAccount)
            {
                results.Add(ToResult(row, "Skipped", "An account with this email already exists."));
                continue;
            }

            bool existingStudentCode = await _context.Users
                .AnyAsync(
                    user => user.StudentCode != null && user.StudentCode.ToLower() == studentCode.ToLower(),
                    cancellationToken);
            if (existingStudentCode)
            {
                results.Add(ToResult(row, "Skipped", "An account with this MSSV already exists."));
                continue;
            }

            string temporaryPassword = GenerateTemporaryPassword();
            var user = new User
            {
                FullName = fullName,
                Email = normalizedEmail,
                StudentCode = studentCode,
                PasswordHash = _passwordHashService.HashPassword(temporaryPassword),
                Role = UserRoles.Student
            };

            _context.Users.Add(user);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _emailSender.SendStudentWelcomeEmailAsync(
                    normalizedEmail,
                    fullName,
                    temporaryPassword,
                    cancellationToken);
                results.Add(ToResult(row, "Created", "Account created and email sent."));
            }
            catch (Exception ex) when (ex is InvalidOperationException or SmtpException)
            {
                _context.Users.Remove(user);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                results.Add(ToResult(row, "Failed", "Account was not created because the welcome email could not be sent."));
            }
            catch (DbUpdateException)
            {
                _context.Entry(user).State = EntityState.Detached;
                results.Add(ToResult(row, "Failed", "Account could not be saved."));
            }
        }

        return new StudentAccountImportResultDto(
            rows.Count,
            results.Count(result => result.Status == "Created"),
            results.Count(result => result.Status == "Skipped"),
            results.Count(result => result.Status == "Failed"),
            results);
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

    public async Task<ResetAccountPasswordResultDto> ResetAccountPasswordAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new ResetAccountPasswordResultDto(false, "Account email is required.");
        }

        User? user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return new ResetAccountPasswordResultDto(false, "Account was not found.");
        }

        if (!IsResettableRole(user.Role))
        {
            return new ResetAccountPasswordResultDto(false, "Only student and teacher accounts can be reset here.");
        }

        if (user.IsBlocked)
        {
            return new ResetAccountPasswordResultDto(false, "Blocked accounts cannot be reset.");
        }

        string temporaryPassword = GenerateTemporaryPassword();

        try
        {
            await _emailSender.SendPasswordResetEmailAsync(
                user.Email,
                user.FullName,
                temporaryPassword,
                cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SmtpException)
        {
            return new ResetAccountPasswordResultDto(false, "Password was not reset because the reset email could not be sent.");
        }

        user.PasswordHash = _passwordHashService.HashPassword(temporaryPassword);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResetAccountPasswordResultDto(true, null);
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
                PasswordHash = _passwordHashService.HashPassword(seed.Password),
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

        if (!_passwordHashService.VerifyPassword(seed.Password, user.PasswordHash))
        {
            user.PasswordHash = _passwordHashService.HashPassword(seed.Password);
            changed = true;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static string? ValidateStudentImportRow(string studentCode, string fullName, string email)
    {
        if (string.IsNullOrWhiteSpace(studentCode))
        {
            return "MSSV is required.";
        }

        if (studentCode.Length > 50)
        {
            return "MSSV cannot exceed 50 characters.";
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "Name is required.";
        }

        if (fullName.Length > 255)
        {
            return "Name cannot exceed 255 characters.";
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return "Email is required.";
        }

        if (email.Length > 255)
        {
            return "Email cannot exceed 255 characters.";
        }

        try
        {
            _ = new MailAddress(email);
        }
        catch (FormatException)
        {
            return "Email is invalid.";
        }

        return null;
    }

    private static StudentAccountImportRowResultDto ToResult(
        StudentImportRowDto row,
        string status,
        string message) =>
        new(row.RowNumber, row.StudentCode, row.FullName, row.Email, status, message);

    private static bool IsResettableRole(string? role) =>
        string.Equals(role, UserRoles.Student, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, UserRoles.Teacher, StringComparison.OrdinalIgnoreCase);

    private static string GenerateTemporaryPassword()
    {
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@$%*-_";
        string allCharacters = lower + upper + digits + symbols;

        var characters = new List<char>
        {
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };

        while (characters.Count < GeneratedPasswordLength)
        {
            characters.Add(allCharacters[RandomNumberGenerator.GetInt32(allCharacters.Length)]);
        }

        for (int i = characters.Count - 1; i > 0; i--)
        {
            int swapIndex = RandomNumberGenerator.GetInt32(i + 1);
            (characters[i], characters[swapIndex]) = (characters[swapIndex], characters[i]);
        }

        return new string(characters.ToArray());
    }
}
