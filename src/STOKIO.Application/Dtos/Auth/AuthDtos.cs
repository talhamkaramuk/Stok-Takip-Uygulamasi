using STOKIO.Domain.Enums;

namespace STOKIO.Application.Dtos.Auth;

public sealed record RegisterTenantRequest(
    string BusinessName,
    string TenantSlug,
    string OwnerName,
    string Email,
    string Password,
    string? TaxNumber = null,
    string? Phone = null);

public sealed record LoginRequest(
    string TenantSlug,
    string Email,
    string Password);

public sealed record UserProfile(
    Guid Id,
    Guid TenantId,
    string TenantSlug,
    string FullName,
    string Email,
    UserRole Role);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserProfile User);
