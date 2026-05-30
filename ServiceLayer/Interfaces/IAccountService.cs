using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IAccountService
{
    Task<AuthenticatedUserDto?> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
