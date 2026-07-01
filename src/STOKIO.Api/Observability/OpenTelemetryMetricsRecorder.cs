using System.Diagnostics;
using System.Diagnostics.Metrics;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Observability;
using STOKIO.Domain.Enums;

namespace STOKIO.Api.Observability;

public sealed class OpenTelemetryMetricsRecorder : IMetricsRecorder, IDisposable
{
    public const string MeterName = "STOKIO.Api";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestLatency;
    private readonly Counter<long> _clientErrorCounter;
    private readonly Counter<long> _serverErrorCounter;
    private readonly Counter<long> _legacyApiRequestCounter;
    private readonly Counter<long> _loginCounter;
    private readonly Counter<long> _stockMovementCounter;
    private readonly Counter<long> _criticalStockMovementCounter;
    private readonly Counter<long> _exportCounter;
    private readonly Histogram<double> _exportDuration;
    private readonly Counter<long> _databaseReadinessCounter;
    private readonly Histogram<double> _databaseReadinessLatency;

    public OpenTelemetryMetricsRecorder()
    {
        _requestCounter = _meter.CreateCounter<long>(
            "stokio.http.requests",
            description: "Total HTTP requests handled by STOKIO.");
        _requestLatency = _meter.CreateHistogram<double>(
            "stokio.http.request.duration",
            unit: "ms",
            description: "HTTP request latency in milliseconds.");
        _clientErrorCounter = _meter.CreateCounter<long>(
            "stokio.http.client_errors",
            description: "HTTP requests completed with 4xx status codes.");
        _serverErrorCounter = _meter.CreateCounter<long>(
            "stokio.http.server_errors",
            description: "HTTP requests completed with 5xx status codes.");
        _legacyApiRequestCounter = _meter.CreateCounter<long>(
            "stokio.legacy_api.requests",
            description: "Legacy /api requests that should migrate to /api/v1 before sunset.");
        _loginCounter = _meter.CreateCounter<long>(
            "stokio.auth.logins",
            description: "Login attempts by outcome.");
        _stockMovementCounter = _meter.CreateCounter<long>(
            "stokio.stock.movements",
            description: "Stock movement events.");
        _criticalStockMovementCounter = _meter.CreateCounter<long>(
            "stokio.stock.critical_movements",
            description: "Stock movements leaving the product at or below critical stock level.");
        _exportCounter = _meter.CreateCounter<long>(
            "stokio.exports",
            description: "Export jobs by type and outcome.");
        _exportDuration = _meter.CreateHistogram<double>(
            "stokio.export.duration",
            unit: "ms",
            description: "Export job processing duration in milliseconds.");
        _databaseReadinessCounter = _meter.CreateCounter<long>(
            "stokio.db.readiness",
            description: "Database readiness checks by outcome.");
        _databaseReadinessLatency = _meter.CreateHistogram<double>(
            "stokio.db.readiness.duration",
            unit: "ms",
            description: "Database readiness check latency in milliseconds.");
    }

    public void RecordRequest(int statusCode, double elapsedMs)
    {
        var statusClass = StatusClass(statusCode);
        var tags = new TagList
        {
            { "http.response.status_code", statusCode },
            { "http.response.status_class", statusClass }
        };

        _requestCounter.Add(1, tags);
        _requestLatency.Record(elapsedMs, tags);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _serverErrorCounter.Add(1, tags);
        }
        else if (statusCode >= StatusCodes.Status400BadRequest)
        {
            _clientErrorCounter.Add(1, tags);
        }
    }

    public void RecordLegacyApiRequest(string method, string route, string client, DateTimeOffset observedAtUtc)
    {
        _ = client;
        _ = observedAtUtc;

        var tags = new TagList
        {
            { "http.request.method", method },
            { "http.route", route }
        };

        _legacyApiRequestCounter.Add(1, tags);
    }

    public void RecordLogin(bool succeeded)
    {
        _loginCounter.Add(1, OutcomeTags(succeeded));
    }

    public void RecordStockMovement(StockMovementType type, int quantity, bool isCriticalAfterMovement)
    {
        _ = quantity;

        var tags = new TagList
        {
            { "stock.movement.type", type.ToString() },
            { "stock.critical_after_movement", isCriticalAfterMovement }
        };

        _stockMovementCounter.Add(1, tags);

        if (isCriticalAfterMovement)
        {
            _criticalStockMovementCounter.Add(1, tags);
        }
    }

    public void RecordExport(ExportJobType type, bool succeeded, double elapsedMs)
    {
        var tags = new TagList
        {
            { "export.type", type.ToString() },
            { "export.outcome", Outcome(succeeded) }
        };

        _exportCounter.Add(1, tags);
        _exportDuration.Record(elapsedMs, tags);
    }

    public void RecordDatabaseReadiness(bool succeeded, double elapsedMs)
    {
        var tags = OutcomeTags(succeeded);
        _databaseReadinessCounter.Add(1, tags);
        _databaseReadinessLatency.Record(elapsedMs, tags);
    }

    public MetricsSnapshotDto Snapshot()
    {
        throw new NotSupportedException("Metrics snapshots are only available when the debug in-memory recorder is enabled.");
    }

    public LegacyApiUsageReportDto LegacyApiUsageReport(DateTimeOffset generatedAtUtc)
    {
        _ = generatedAtUtc;
        throw new NotSupportedException("Legacy API usage reports are only available when the debug in-memory recorder is enabled.");
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    private static TagList OutcomeTags(bool succeeded)
    {
        return new TagList
        {
            { "outcome", Outcome(succeeded) }
        };
    }

    private static string Outcome(bool succeeded)
    {
        return succeeded ? "success" : "failure";
    }

    private static string StatusClass(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => "5xx",
            >= 400 => "4xx",
            >= 300 => "3xx",
            >= 200 => "2xx",
            >= 100 => "1xx",
            _ => "unknown"
        };
    }
}
