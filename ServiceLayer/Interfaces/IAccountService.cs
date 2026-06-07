using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IAccountService
{
    Task<AuthenticatedUserDto?> AuthenticateAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<ChangePasswordResultDto> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
}
