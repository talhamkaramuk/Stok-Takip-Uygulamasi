using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Auth;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Security;

namespace STOKIO.Infrastructure.Services;

public sealed class AuthService(
    StokioDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IClock clock,
    IOptions<AuthOptions> authOptions) : IAuthService
{
    public async Task<AuthSession> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken cancellationToken)
    {
        var tenantSlug = Slugs.Normalize(request.TenantSlug);
        var email = NormalizeEmail(request.Email);

        var tenantExists = await dbContext.Tenants.AnyAsync(x => x.Slug == tenantSlug, cancellationToken);
        if (tenantExists)
        {
            throw new AppProblemException(409, "tenant_slug_exists", "This tenant slug is already in use.");
        }

        var tenant = new Tenant
        {
            Name = request.BusinessName.Trim(),
            Slug = tenantSlug,
            TaxNumber = string.IsNullOrWhiteSpace(request.TaxNumber) ? null : request.TaxNumber.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim()
        };

        var user = new ApplicationUser
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            FullName = request.OwnerName.Trim(),
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRole.Owner
        };

        dbContext.Tenants.Add(tenant);
        dbContext.ApplicationUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await CreateSessionAsync(user, tenant, cancellationToken);
    }

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var tenantSlug = Slugs.Normalize(request.TenantSlug);
        var email = NormalizeEmail(request.Email);

        var user = await dbContext.ApplicationUsers
            .IgnoreQueryFilters()
            .Include(x => x.Tenant)
            .SingleOrDefaultAsync(
                x => x.Tenant.Slug == tenantSlug && x.Email == email && x.IsActive && x.Tenant.IsActive,
                cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AppProblemException(401, "invalid_credentials", "Invalid tenant, email, or password.");
        }

        return await CreateSessionAsync(user, user.Tenant, cancellationToken);
    }

    public async Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw InvalidRefreshToken();
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Include(x => x.User)
            .ThenInclude(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            throw InvalidRefreshToken();
        }

        var now = clock.UtcNow;
        if (storedToken.RevokedAt is not null || storedToken.ExpiresAt <= now)
        {
            if (storedToken.RevokedAt is null)
            {
                Revoke(storedToken, now, "expired");
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            throw InvalidRefreshToken();
        }

        if (!storedToken.User.IsActive || !storedToken.User.Tenant.IsActive)
        {
            Revoke(storedToken, now, "inactive_principal");
            await dbContext.SaveChangesAsync(cancellationToken);
            throw InvalidRefreshToken();
        }

        return await RotateSessionAsync(storedToken, now, cancellationToken);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.RevokedAt is not null)
        {
            return;
        }

        Revoke(storedToken, clock.UtcNow, "logout");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthSession> RotateSessionAsync(
        RefreshToken storedToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var newRefreshToken = CreateRefreshToken(storedToken.User, storedToken.User.Tenant);
        Revoke(storedToken, now, "rotated");
        storedToken.ReplacedByTokenHash = newRefreshToken.TokenHash;

        dbContext.RefreshTokens.Add(newRefreshToken.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthSession(
            jwtTokenService.CreateToken(storedToken.User, storedToken.User.Tenant),
            newRefreshToken.RawToken,
            newRefreshToken.Entity.ExpiresAt);
    }

    private async Task<AuthSession> CreateSessionAsync(
        ApplicationUser user,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var refreshToken = CreateRefreshToken(user, tenant);
        dbContext.RefreshTokens.Add(refreshToken.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthSession(
            jwtTokenService.CreateToken(user, tenant),
            refreshToken.RawToken,
            refreshToken.Entity.ExpiresAt);
    }

    private CreatedRefreshToken CreateRefreshToken(ApplicationUser user, Tenant tenant)
    {
        var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var entity = new RefreshToken
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            UserId = user.Id,
            User = user,
            TokenHash = HashRefreshToken(rawToken),
            ExpiresAt = clock.UtcNow.AddDays(authOptions.Value.RefreshTokenDays)
        };

        return new CreatedRefreshToken(rawToken, entity);
    }

    private static string HashRefreshToken(string refreshToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    private static void Revoke(RefreshToken refreshToken, DateTimeOffset now, string reason)
    {
        refreshToken.RevokedAt = now;
        refreshToken.RevocationReason = reason;
    }

    private static AppProblemException InvalidRefreshToken()
    {
        return new AppProblemException(401, "invalid_refresh_token", "Refresh session is invalid or expired.");
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private sealed record CreatedRefreshToken(string RawToken, RefreshToken Entity)
    {
        public string TokenHash => Entity.TokenHash;
    }
}
