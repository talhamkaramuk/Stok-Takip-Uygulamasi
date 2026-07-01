using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Observability;
using STOKIO.Api.Middleware;
using STOKIO.Domain.Enums;

namespace STOKIO.Api.Observability;

public sealed class InMemoryMetricsRecorder : IMetricsRecorder
{
    private const int MaxLegacyApiClients = 256;
    private const string OtherLegacyApiClient = "other";

    private long _requestCount;
    private long _clientErrorCount;
    private long _serverErrorCount;
    private double _requestLatencyTotalMs;
    private long _legacyApiRequestCount;
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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, LegacyApiClientUsage> _legacyApiClients =
        new(StringComparer.Ordinal);

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

    public void RecordLegacyApiRequest(string method, string route, string client, DateTimeOffset observedAtUtc)
    {
        Interlocked.Increment(ref _legacyApiRequestCount);

        var normalizedClient = NormalizeDimension(client, OtherLegacyApiClient, maxLength: 120);
        if (!_legacyApiClients.ContainsKey(normalizedClient)
            && _legacyApiClients.Count >= MaxLegacyApiClients
            && !string.Equals(normalizedClient, OtherLegacyApiClient, StringComparison.Ordinal))
        {
            normalizedClient = OtherLegacyApiClient;
        }

        var usage = _legacyApiClients.GetOrAdd(normalizedClient, _ => new LegacyApiClientUsage());
        usage.Record(
            NormalizeDimension(method, "UNKNOWN", maxLength: 16).ToUpperInvariant(),
            NormalizeDimension(route, "unknown", maxLength: 160),
            observedAtUtc);
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
            Average(_databaseReadinessTotalMs, databaseReadinessCount),
            Interlocked.Read(ref _legacyApiRequestCount));
    }

    public LegacyApiUsageReportDto LegacyApiUsageReport(DateTimeOffset generatedAtUtc)
    {
        var clients = _legacyApiClients
            .Select(pair => pair.Value.Snapshot(pair.Key))
            .OrderByDescending(client => client.RequestCount)
            .ThenBy(client => client.Client, StringComparer.Ordinal)
            .ToArray();

        return new LegacyApiUsageReportDto(
            generatedAtUtc,
            LegacyApiDeprecationMiddleware.SunsetAtUtc,
            LegacyApiDeprecationMiddleware.RemoveMappingsAfterUtc,
            Interlocked.Read(ref _legacyApiRequestCount),
            clients);
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

    private static string NormalizeDimension(string? value, string fallback, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed class LegacyApiClientUsage
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, LegacyApiRouteUsage> _routes = new(StringComparer.Ordinal);
        private long _requestCount;
        private DateTimeOffset _firstSeenAtUtc = DateTimeOffset.MaxValue;
        private DateTimeOffset _lastSeenAtUtc = DateTimeOffset.MinValue;

        public void Record(string method, string route, DateTimeOffset observedAtUtc)
        {
            lock (_gate)
            {
                _requestCount++;
                if (observedAtUtc < _firstSeenAtUtc)
                {
                    _firstSeenAtUtc = observedAtUtc;
                }

                if (observedAtUtc > _lastSeenAtUtc)
                {
                    _lastSeenAtUtc = observedAtUtc;
                }

                var routeKey = $"{method} {route}";
                if (!_routes.TryGetValue(routeKey, out var usage))
                {
                    usage = new LegacyApiRouteUsage(method, route);
                    _routes.Add(routeKey, usage);
                }

                usage.Record(observedAtUtc);
            }
        }

        public LegacyApiClientUsageDto Snapshot(string client)
        {
            lock (_gate)
            {
                return new LegacyApiClientUsageDto(
                    client,
                    _requestCount,
                    _firstSeenAtUtc == DateTimeOffset.MaxValue ? DateTimeOffset.MinValue : _firstSeenAtUtc,
                    _lastSeenAtUtc == DateTimeOffset.MinValue ? DateTimeOffset.MinValue : _lastSeenAtUtc,
                    _routes.Values
                        .Select(route => route.Snapshot())
                        .OrderByDescending(route => route.RequestCount)
                        .ThenBy(route => route.Method, StringComparer.Ordinal)
                        .ThenBy(route => route.Route, StringComparer.Ordinal)
                        .ToArray());
            }
        }
    }

    private sealed class LegacyApiRouteUsage(string method, string route)
    {
        private long _requestCount;
        private DateTimeOffset _lastSeenAtUtc = DateTimeOffset.MinValue;

        public void Record(DateTimeOffset observedAtUtc)
        {
            _requestCount++;
            if (observedAtUtc > _lastSeenAtUtc)
            {
                _lastSeenAtUtc = observedAtUtc;
            }
        }

        public LegacyApiRouteUsageDto Snapshot()
        {
            return new LegacyApiRouteUsageDto(method, route, _requestCount, _lastSeenAtUtc);
        }
    }
}
