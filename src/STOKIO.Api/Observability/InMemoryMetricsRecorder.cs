using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Observability;
using STOKIO.Domain.Enums;

namespace STOKIO.Api.Observability;

public sealed class InMemoryMetricsRecorder : IMetricsRecorder
{
    private long _requestCount;
    private long _clientErrorCount;
    private long _serverErrorCount;
    private double _requestLatencyTotalMs;
    private long _loginSuccessCount;
    private long _loginFailureCount;
    private long _stockMovementCount;
    private long _criticalStockMovementCount;
    private long _exportSuccessCount;
    private long _exportFailureCount;
    private double _exportDurationTotalMs;
    private long _databaseReadinessSuccessCount;
    private long _databaseReadinessFailureCount;
    private double _databaseReadinessTotalMs;

    public void RecordRequest(int statusCode, double elapsedMs)
    {
        Interlocked.Increment(ref _requestCount);
        Add(ref _requestLatencyTotalMs, elapsedMs);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            Interlocked.Increment(ref _serverErrorCount);
        }
        else if (statusCode >= StatusCodes.Status400BadRequest)
        {
            Interlocked.Increment(ref _clientErrorCount);
        }
    }

    public void RecordLogin(bool succeeded)
    {
        if (succeeded)
        {
            Interlocked.Increment(ref _loginSuccessCount);
            return;
        }

        Interlocked.Increment(ref _loginFailureCount);
    }

    public void RecordStockMovement(StockMovementType type, int quantity, bool isCriticalAfterMovement)
    {
        _ = type;
        _ = quantity;
        Interlocked.Increment(ref _stockMovementCount);
        if (isCriticalAfterMovement)
        {
            Interlocked.Increment(ref _criticalStockMovementCount);
        }
    }

    public void RecordExport(ExportJobType type, bool succeeded, double elapsedMs)
    {
        _ = type;
        Add(ref _exportDurationTotalMs, elapsedMs);
        if (succeeded)
        {
            Interlocked.Increment(ref _exportSuccessCount);
            return;
        }

        Interlocked.Increment(ref _exportFailureCount);
    }

    public void RecordDatabaseReadiness(bool succeeded, double elapsedMs)
    {
        Add(ref _databaseReadinessTotalMs, elapsedMs);
        if (succeeded)
        {
            Interlocked.Increment(ref _databaseReadinessSuccessCount);
            return;
        }

        Interlocked.Increment(ref _databaseReadinessFailureCount);
    }

    public MetricsSnapshotDto Snapshot()
    {
        var requestCount = Interlocked.Read(ref _requestCount);
        var exportCount = Interlocked.Read(ref _exportSuccessCount) + Interlocked.Read(ref _exportFailureCount);
        var databaseReadinessCount = Interlocked.Read(ref _databaseReadinessSuccessCount) + Interlocked.Read(ref _databaseReadinessFailureCount);

        return new MetricsSnapshotDto(
            requestCount,
            Interlocked.Read(ref _clientErrorCount),
            Interlocked.Read(ref _serverErrorCount),
            Average(_requestLatencyTotalMs, requestCount),
            Interlocked.Read(ref _loginSuccessCount),
            Interlocked.Read(ref _loginFailureCount),
            Interlocked.Read(ref _stockMovementCount),
            Interlocked.Read(ref _criticalStockMovementCount),
            Interlocked.Read(ref _exportSuccessCount),
            Interlocked.Read(ref _exportFailureCount),
            Average(_exportDurationTotalMs, exportCount),
            Interlocked.Read(ref _databaseReadinessSuccessCount),
            Interlocked.Read(ref _databaseReadinessFailureCount),
            Average(_databaseReadinessTotalMs, databaseReadinessCount));
    }

    private static void Add(ref double location, double value)
    {
        double initialValue;
        double computedValue;
        do
        {
            initialValue = location;
            computedValue = initialValue + value;
        }
        while (Math.Abs(Interlocked.CompareExchange(ref location, computedValue, initialValue) - initialValue) > double.Epsilon);
    }

    private static double Average(double total, long count)
    {
        return count <= 0 ? 0 : Math.Round(total / count, 2);
    }
}
