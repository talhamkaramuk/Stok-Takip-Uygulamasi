using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using STOKIO.Api.Observability;
using STOKIO.Domain.Enums;

namespace STOKIO.Tests.Observability;

public sealed class OpenTelemetryMetricsRecorderTests
{
    [Fact]
    public void Recorder_PublishesRequiredMetricInstruments()
    {
        using var listener = new MeterListener();
        var measurements = new ConcurrentBag<CapturedMeasurement>();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == OpenTelemetryMetricsRecorder.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            measurements.Add(Capture(instrument, measurement, tags)));
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            measurements.Add(Capture(instrument, measurement, tags)));
        listener.Start();

        using var recorder = new OpenTelemetryMetricsRecorder();

        recorder.RecordRequest(StatusCodes.Status404NotFound, 12.5);
        recorder.RecordRequest(StatusCodes.Status503ServiceUnavailable, 25);
        recorder.RecordLegacyApiRequest("GET", "/api/products", "mobile-app", DateTimeOffset.UtcNow);
        recorder.RecordLogin(succeeded: true);
        recorder.RecordLogin(succeeded: false);
        recorder.RecordStockMovement(StockMovementType.Out, quantity: 3, isCriticalAfterMovement: true);
        recorder.RecordExport(ExportJobType.CurrentStock, succeeded: true, elapsedMs: 42);
        recorder.RecordExport(ExportJobType.CriticalStock, succeeded: false, elapsedMs: 84);
        recorder.RecordDatabaseReadiness(succeeded: true, elapsedMs: 8);
        recorder.RecordDatabaseReadiness(succeeded: false, elapsedMs: 16);

        var names = measurements.Select(measurement => measurement.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("stokio.http.requests", names);
        Assert.Contains("stokio.http.request.duration", names);
        Assert.Contains("stokio.http.client_errors", names);
        Assert.Contains("stokio.http.server_errors", names);
        Assert.Contains("stokio.legacy_api.requests", names);
        Assert.Contains("stokio.auth.logins", names);
        Assert.Contains("stokio.stock.movements", names);
        Assert.Contains("stokio.stock.critical_movements", names);
        Assert.Contains("stokio.exports", names);
        Assert.Contains("stokio.export.duration", names);
        Assert.Contains("stokio.db.readiness", names);
        Assert.Contains("stokio.db.readiness.duration", names);
        Assert.Contains(measurements, measurement =>
            measurement.Name == "stokio.http.client_errors"
            && measurement.Tags.TryGetValue("http.response.status_class", out var statusClass)
            && statusClass == "4xx");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "stokio.legacy_api.requests"
            && measurement.Tags.TryGetValue("http.route", out var route)
            && route == "/api/products");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "stokio.auth.logins"
            && measurement.Tags.TryGetValue("outcome", out var outcome)
            && outcome == "failure");
        Assert.Contains(measurements, measurement =>
            measurement.Name == "stokio.exports"
            && measurement.Tags.TryGetValue("export.outcome", out var outcome)
            && outcome == "failure");
    }

    private static CapturedMeasurement Capture<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
        where T : struct
    {
        var capturedTags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            capturedTags[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }

        return new CapturedMeasurement(instrument.Name, measurement.ToString() ?? string.Empty, capturedTags);
    }

    private sealed record CapturedMeasurement(
        string Name,
        string Value,
        IReadOnlyDictionary<string, string> Tags);
}
