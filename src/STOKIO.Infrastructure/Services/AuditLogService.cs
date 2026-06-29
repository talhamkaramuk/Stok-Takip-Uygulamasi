using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Observability;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class AuditLogService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant) : IAuditLogService
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(
        string? search,
        string? action,
        string? entityName,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        EnsureTenant();
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Action.ToLower().Contains(term) ||
                x.EntityName.ToLower().Contains(term) ||
                x.EntityId.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(x => x.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var normalizedEntityName = entityName.Trim();
            query = query.Where(x => x.EntityName == normalizedEntityName);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new AuditLogDto(
                x.Id,
                x.UserId,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.OldValueJson,
                x.NewValueJson,
                x.MetadataJson,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(logs, normalizedPage, normalizedPageSize, totalCount);
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant bağlamı gerekli.");
        }
    }

}
