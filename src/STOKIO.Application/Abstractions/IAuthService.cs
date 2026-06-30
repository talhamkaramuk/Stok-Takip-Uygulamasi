using STOKIO.Application.Dtos.Auth;

namespace STOKIO.Application.Abstractions;

public interface IAuthService
{
    Task<AuthSession> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken cancellationToken);
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
}
