using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ServiceLayer.Services;

public sealed class AccountService : IAccountService
{
    private readonly Prn222Context _context;
    private readonly IPasswordHashService _passwordHashService;

    public AccountService(Prn222Context context, IPasswordHashService passwordHashService)
    {
        _context = context;
        _passwordHashService = passwordHashService;
    }

    public async Task<AuthenticatedUserDto?> AuthenticateAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = request.Email.Trim().ToLowerInvariant();
        string submittedPassword = request.Password;
        string trimmedPassword = request.Password.Trim();

        User? user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null || user.IsBlocked)
        {
            return null;
        }

        bool passwordVerified = _passwordHashService.VerifyPassword(submittedPassword, user.PasswordHash);
        string passwordForLegacyUpgrade = submittedPassword;

        if (!passwordVerified
            && !string.Equals(submittedPassword, trimmedPassword, StringComparison.Ordinal)
            && _passwordHashService.VerifyPassword(trimmedPassword, user.PasswordHash))
        {
            passwordVerified = true;
        }

        if (!passwordVerified && IsLegacyPlainTextPassword(user.PasswordHash, submittedPassword))
        {
            passwordVerified = true;
        }
        else if (!passwordVerified
            && !string.Equals(submittedPassword, trimmedPassword, StringComparison.Ordinal)
            && IsLegacyPlainTextPassword(user.PasswordHash, trimmedPassword))
        {
            passwordForLegacyUpgrade = trimmedPassword;
            passwordVerified = true;
        }

        if (passwordVerified && !user.PasswordHash.StartsWith("PBKDF2$", StringComparison.Ordinal))
        {
            user.PasswordHash = _passwordHashService.HashPassword(passwordForLegacyUpgrade);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (!passwordVerified)
        {
            return null;
        }

        return new AuthenticatedUserDto(
            user.UserId,
            user.FullName,
            user.Email,
            user.Role ?? UserRoles.Student);
    }

    public async Task<ChangePasswordResultDto> ChangePasswordAsync(
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId == Guid.Empty)
        {
            return new ChangePasswordResultDto(false, "Your session is invalid. Please sign in again.");
        }

        User? user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

        if (user is null || user.IsBlocked)
        {
            return new ChangePasswordResultDto(false, "Your account is not available.");
        }

        if (!_passwordHashService.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return new ChangePasswordResultDto(false, "Current password is incorrect.");
        }

        if (_passwordHashService.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            return new ChangePasswordResultDto(false, "New password must be different from the current password.");
        }

        user.PasswordHash = _passwordHashService.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);

        return new ChangePasswordResultDto(true, null);
    }

    private static bool IsLegacyPlainTextPassword(string storedPassword, string submittedPassword)
    {
        if (string.IsNullOrEmpty(storedPassword)
            || string.IsNullOrEmpty(submittedPassword)
            || storedPassword.StartsWith("PBKDF2$", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] storedBytes = Encoding.UTF8.GetBytes(storedPassword);
        byte[] submittedBytes = Encoding.UTF8.GetBytes(submittedPassword);

        return storedBytes.Length == submittedBytes.Length
            && CryptographicOperations.FixedTimeEquals(storedBytes, submittedBytes);
    }
}
