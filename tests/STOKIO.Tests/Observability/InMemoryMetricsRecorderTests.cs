using STOKIO.Api.Middleware;
using STOKIO.Api.Observability;

namespace STOKIO.Tests.Observability;

public sealed class InMemoryMetricsRecorderTests
{
    [Fact]
    public void LegacyApiUsageReport_GroupsUsageByClientAndRoute()
    {
        var recorder = new InMemoryMetricsRecorder();
        var firstSeen = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

        recorder.RecordLegacyApiRequest("get", "/api/products", "mobile-app", firstSeen);
        recorder.RecordLegacyApiRequest("GET", "/api/products", "mobile-app", firstSeen.AddMinutes(1));
        recorder.RecordLegacyApiRequest("POST", "/api/orders", "warehouse-terminal", firstSeen.AddMinutes(2));

        var snapshot = recorder.Snapshot();
        var report = recorder.LegacyApiUsageReport(firstSeen.AddHours(1));

        Assert.Equal(3, snapshot.LegacyApiRequestCount);
        Assert.Equal(3, report.TotalRequestCount);
        Assert.Equal(LegacyApiDeprecationMiddleware.SunsetAtUtc, report.SunsetAtUtc);
        Assert.Equal(LegacyApiDeprecationMiddleware.RemoveMappingsAfterUtc, report.RemoveMappingsAfterUtc);

        var mobileClient = Assert.Single(report.Clients, client => client.Client == "mobile-app");
        Assert.Equal(2, mobileClient.RequestCount);
        Assert.Equal(firstSeen, mobileClient.FirstSeenAtUtc);
        Assert.Equal(firstSeen.AddMinutes(1), mobileClient.LastSeenAtUtc);

        var mobileRoute = Assert.Single(mobileClient.Routes);
        Assert.Equal("GET", mobileRoute.Method);
        Assert.Equal("/api/products", mobileRoute.Route);
        Assert.Equal(2, mobileRoute.RequestCount);
        Assert.Equal(firstSeen.AddMinutes(1), mobileRoute.LastSeenAtUtc);
    }

    [Fact]
    public void LegacyRouteMapping_IsDisabledAfterSunset()
    {
        Assert.True(LegacyApiDeprecationMiddleware.ShouldMapLegacyRoutes(LegacyApiDeprecationMiddleware.RemoveMappingsAfterUtc));
        Assert.False(LegacyApiDeprecationMiddleware.ShouldMapLegacyRoutes(LegacyApiDeprecationMiddleware.RemoveMappingsAfterUtc.AddSeconds(1)));
    }
}
