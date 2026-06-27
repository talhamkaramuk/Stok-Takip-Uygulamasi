using STOKIO.Application.Dtos.Reports;

namespace STOKIO.Application.Abstractions;

public interface IReportService
{
    Task<IReadOnlyList<CurrentStockReportRow>> CurrentStockAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<MovementReportRow>> MovementsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);
    Task<IReadOnlyList<CountDifferenceReportRow>> CountDifferencesAsync(Guid countId, CancellationToken cancellationToken);
}

