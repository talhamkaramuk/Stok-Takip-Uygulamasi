using STOKIO.Application.Dtos.Exports;

namespace STOKIO.Application.Abstractions;

public interface IExportService
{
    Task<ExportFile> CurrentStockAsync(CancellationToken cancellationToken);
    Task<ExportFile> CriticalStockAsync(CancellationToken cancellationToken);
    Task<ExportFile> MovementsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken);
    Task<ExportFile> CountDifferencesAsync(Guid countId, CancellationToken cancellationToken);
}
