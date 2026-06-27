using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Users;

namespace STOKIO.Application.Abstractions;

public interface IUserManagementService
{
    Task<PagedResult<UserDto>> ListAsync(bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

