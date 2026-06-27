using STOKIO.Application.Dtos.Auth;
using STOKIO.Domain.Entities;

namespace STOKIO.Application.Abstractions;

public interface IJwtTokenService
{
    AuthResponse CreateToken(ApplicationUser user, Tenant tenant);
}

