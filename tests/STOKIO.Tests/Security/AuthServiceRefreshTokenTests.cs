using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Auth;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Security;
using STOKIO.Infrastructure.Services;
using STOKIO.Tests.Common;

namespace STOKIO.Tests.Security;

public sealed class AuthServiceRefreshTokenTests
{
    [Fact]
    public async Task LoginAsync_IssuesRefreshTokenWithoutPersistingRawToken()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        await SeedUserAsync(dbContext);

        var session = await service.LoginAsync(
            new LoginRequest("test-tenant", "OWNER@stokio.local", "StrongPass123"),
            CancellationToken.None);

        var storedToken = Assert.Single(dbContext.RefreshTokens);
        Assert.False(string.IsNullOrWhiteSpace(session.Response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.NotEqual(session.RefreshToken, storedToken.TokenHash);
        Assert.Equal(64, storedToken.TokenHash.Length);
        Assert.Equal(new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero), storedToken.ExpiresAt);
        Assert.Null(storedToken.RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_RotatesRefreshTokenAndRejectsPreviousToken()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var originalSession = await service.RegisterTenantAsync(
            new RegisterTenantRequest("Test Tenant", "test-tenant", "Owner", "owner@stokio.local", "StrongPass123"),
            CancellationToken.None);

        var rotatedSession = await service.RefreshAsync(originalSession.RefreshToken, CancellationToken.None);

        Assert.NotEqual(originalSession.RefreshToken, rotatedSession.RefreshToken);
        var tokens = dbContext.RefreshTokens.ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.Single(tokens, token => token.RevokedAt is not null && token.RevocationReason == "rotated");
        Assert.Single(tokens, token => token.RevokedAt is null);

        var exception = await Assert.ThrowsAsync<AppProblemException>(() =>
            service.RefreshAsync(originalSession.RefreshToken, CancellationToken.None));
        Assert.Equal(401, exception.StatusCode);
        Assert.Equal("invalid_refresh_token", exception.Code);
    }

    [Fact]
    public async Task LogoutAsync_RevokesRefreshToken()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        await SeedUserAsync(dbContext);
        var session = await service.LoginAsync(
            new LoginRequest("test-tenant", "owner@stokio.local", "StrongPass123"),
            CancellationToken.None);

        await service.LogoutAsync(session.RefreshToken, CancellationToken.None);

        var storedToken = Assert.Single(dbContext.RefreshTokens);
        Assert.NotNull(storedToken.RevokedAt);
        Assert.Equal("logout", storedToken.RevocationReason);
        await Assert.ThrowsAsync<AppProblemException>(() =>
            service.RefreshAsync(session.RefreshToken, CancellationToken.None));
    }

    private static AuthService CreateService(StokioDbContext dbContext)
    {
        var clock = new TestClock();
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "STOKIO.Api",
            Audience = "STOKIO.Web",
            SigningKey = "test-secret-value-with-at-least-32-bytes",
            AccessTokenMinutes = 15
        });

        return new AuthService(
            dbContext,
            new Pbkdf2PasswordHasher(),
            new JwtTokenService(jwtOptions, clock),
            clock,
            Options.Create(new AuthOptions { RefreshTokenDays = 14 }));
    }

    private static async Task SeedUserAsync(StokioDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Test Tenant",
            Slug = "test-tenant"
        };
        var user = new ApplicationUser
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            FullName = "Owner",
            Email = "owner@stokio.local",
            PasswordHash = new Pbkdf2PasswordHasher().Hash("StrongPass123"),
            Role = UserRole.Owner
        };

        dbContext.Tenants.Add(tenant);
        dbContext.ApplicationUsers.Add(user);
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private static StokioDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StokioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StokioDbContext(options, new NoTenantContext());
    }

    private sealed class NoTenantContext : ICurrentTenant
    {
        public bool HasTenant => false;
        public Guid TenantId => Guid.Empty;
        public string? TenantSlug => null;

        public void SetTenant(Guid tenantId, string? slug)
        {
        }
    }
}
