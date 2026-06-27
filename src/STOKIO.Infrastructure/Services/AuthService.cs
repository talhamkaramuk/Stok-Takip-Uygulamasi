using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Auth;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class AuthService(
    StokioDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken cancellationToken)
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

        return jwtTokenService.CreateToken(user, tenant);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
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

        return jwtTokenService.CreateToken(user, user.Tenant);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
