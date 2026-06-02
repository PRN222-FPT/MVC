using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IUserManagementService
{
    Task<IReadOnlyList<AdminUserListItemDto>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<CreateManagedUserResultDto> CreateUserAsync(
        CreateManagedUserDto request,
        CancellationToken cancellationToken = default);

    Task<BlockManagedUserResultDto> BlockUserAsync(
        Guid userId,
        Guid currentAdminUserId,
        CancellationToken cancellationToken = default);

    Task EnsureAdminUserAsync(AdminUserSeedDto seed, CancellationToken cancellationToken = default);
}
