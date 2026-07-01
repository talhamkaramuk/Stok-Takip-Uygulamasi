using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Observability;
using STOKIO.Domain.Enums;

namespace STOKIO.Api.Observability;

public sealed class CompositeMetricsRecorder(
    OpenTelemetryMetricsRecorder openTelemetryRecorder,
    InMemoryMetricsRecorder inMemoryRecorder) : IMetricsRecorder
{
    public void RecordRequest(int statusCode, double elapsedMs)
    {
        openTelemetryRecorder.RecordRequest(statusCode, elapsedMs);
        inMemoryRecorder.RecordRequest(statusCode, elapsedMs);
    }

    public void RecordLegacyApiRequest(string method, string route, string client, DateTimeOffset observedAtUtc)
    {
        openTelemetryRecorder.RecordLegacyApiRequest(method, route, client, observedAtUtc);
        inMemoryRecorder.RecordLegacyApiRequest(method, route, client, observedAtUtc);
    }

    public void RecordLogin(bool succeeded)
    {
        openTelemetryRecorder.RecordLogin(succeeded);
        inMemoryRecorder.RecordLogin(succeeded);
    }

    public void RecordStockMovement(StockMovementType type, int quantity, bool isCriticalAfterMovement)
    {
        openTelemetryRecorder.RecordStockMovement(type, quantity, isCriticalAfterMovement);
        inMemoryRecorder.RecordStockMovement(type, quantity, isCriticalAfterMovement);
    }

    public void RecordExport(ExportJobType type, bool succeeded, double elapsedMs)
    {
        openTelemetryRecorder.RecordExport(type, succeeded, elapsedMs);
        inMemoryRecorder.RecordExport(type, succeeded, elapsedMs);
    }

    public void RecordDatabaseReadiness(bool succeeded, double elapsedMs)
    {
        openTelemetryRecorder.RecordDatabaseReadiness(succeeded, elapsedMs);
        inMemoryRecorder.RecordDatabaseReadiness(succeeded, elapsedMs);
    }

    public MetricsSnapshotDto Snapshot()
    {
        return inMemoryRecorder.Snapshot();
    }

    public LegacyApiUsageReportDto LegacyApiUsageReport(DateTimeOffset generatedAtUtc)
    {
        return inMemoryRecorder.LegacyApiUsageReport(generatedAtUtc);
    }
}
