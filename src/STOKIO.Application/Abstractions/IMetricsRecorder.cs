using STOKIO.Application.Dtos.Observability;
using STOKIO.Domain.Enums;

namespace STOKIO.Application.Abstractions;

public interface IMetricsRecorder
{
    void RecordRequest(int statusCode, double elapsedMs);
    void RecordLogin(bool succeeded);
    void RecordStockMovement(StockMovementType type, int quantity, bool isCriticalAfterMovement);
    void RecordExport(ExportJobType type, bool succeeded, double elapsedMs);
    void RecordDatabaseReadiness(bool succeeded, double elapsedMs);
    MetricsSnapshotDto Snapshot();
}
