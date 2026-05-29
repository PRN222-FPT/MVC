using DataAccessLayer.Models;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

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

        User? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null || !_passwordHashService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return null;
        }

        return new AuthenticatedUserDto(
            user.UserId,
            user.FullName,
            user.Email,
            user.Role ?? UserRoles.Student);
    }
}
