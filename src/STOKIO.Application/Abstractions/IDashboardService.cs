using STOKIO.Application.Dtos.Dashboard;

namespace STOKIO.Application.Abstractions;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}
