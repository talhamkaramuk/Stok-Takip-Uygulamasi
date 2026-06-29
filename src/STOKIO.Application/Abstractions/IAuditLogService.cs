using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Observability;

namespace STOKIO.Application.Abstractions;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> ListAsync(
        string? search,
        string? action,
        string? entityName,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken);
}
