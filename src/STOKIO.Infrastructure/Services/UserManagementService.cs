using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Users;
using STOKIO.Domain.Entities;
using STOKIO.Domain.Enums;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class UserManagementService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    AuditWriter auditWriter) : IUserManagementService
{
    public async Task<PagedResult<UserDto>> ListAsync(string? search, bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);

        var query = dbContext.ApplicationUsers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.FullName.ToLower().Contains(term) ||
                x.Email.ToLower().Contains(term));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(x => x.FullName)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new UserDto(x.Id, x.FullName, x.Email, x.Role, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>(users, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        EnsureManageableRole(request.Role);
        var email = NormalizeEmail(request.Email);
        await EnsureEmailAvailableAsync(email, null, cancellationToken);

        var user = new ApplicationUser
        {
            TenantId = currentTenant.TenantId,
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = request.Role
        };

        dbContext.ApplicationUsers.Add(user);
        auditWriter.AddSnapshot("user.created", nameof(ApplicationUser), user.Id, null, Snapshot(user));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        EnsureManageableRole(request.Role);
        var user = await FindAsync(id, cancellationToken);
        EnsureUserCanBeManaged(user);
        var oldValue = Snapshot(user);

        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;

        auditWriter.AddSnapshot("user.updated", nameof(ApplicationUser), user.Id, oldValue, Snapshot(user));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var user = await FindAsync(id, cancellationToken);
        EnsureUserCanBeManaged(user);
        var oldValue = Snapshot(user);
        user.IsActive = false;
        auditWriter.AddSnapshot("user.deactivated", nameof(ApplicationUser), user.Id, oldValue, Snapshot(user));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ApplicationUser> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.ApplicationUsers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return user ?? throw new AppProblemException(404, "user_not_found", "User was not found.");
    }

    private async Task EnsureEmailAvailableAsync(string email, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.ApplicationUsers.AnyAsync(
            x => x.Email == email && (!currentUserId.HasValue || x.Id != currentUserId.Value),
            cancellationToken);

        if (exists)
        {
            throw new AppProblemException(409, "email_exists", "A user with this email already exists.");
        }
    }

    private void EnsureUserCanBeManaged(ApplicationUser user)
    {
        if (user.Id == currentUser.UserId)
        {
            throw new AppProblemException(400, "self_user_management_blocked", "Current user cannot be modified from this endpoint.");
        }

        if (user.Role == UserRole.Owner)
        {
            throw new AppProblemException(400, "owner_user_management_blocked", "Owner users cannot be modified from this endpoint.");
        }
    }

    private static void EnsureManageableRole(UserRole role)
    {
        if (role is not (UserRole.Manager or UserRole.Staff))
        {
            throw new AppProblemException(400, "invalid_user_role", "Only Manager and Staff roles can be managed here.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static UserDto ToDto(ApplicationUser user)
    {
        return new UserDto(user.Id, user.FullName, user.Email, user.Role, user.IsActive, user.CreatedAt);
    }

    private static object Snapshot(ApplicationUser user)
    {
        return new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.Role,
            user.IsActive
        };
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }
    }
}
