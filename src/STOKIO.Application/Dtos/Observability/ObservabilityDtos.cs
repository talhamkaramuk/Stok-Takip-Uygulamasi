namespace STOKIO.Application.Dtos.Observability;

public sealed record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string Action,
    string EntityName,
    string EntityId,
    string? OldValueJson,
    string? NewValueJson,
    string? MetadataJson,
    DateTimeOffset CreatedAt);

public sealed record MetricsSnapshotDto(
    long RequestCount,
    long ClientErrorCount,
    long ServerErrorCount,
    double AverageLatencyMs,
    long LoginSuccessCount,
    long LoginFailureCount,
    long StockMovementCount,
    long CriticalStockMovementCount,
    long ExportSuccessCount,
    long ExportFailureCount,
    double AverageExportDurationMs,
    long DatabaseReadinessSuccessCount,
    long DatabaseReadinessFailureCount,
    double AverageDatabaseReadinessMs);
