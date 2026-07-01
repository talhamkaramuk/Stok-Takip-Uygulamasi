using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Endpoints;

public static class ObservabilityEndpoints
{
    public static IEndpointRouteBuilder MapObservabilityEndpoints(
        this IEndpointRouteBuilder app,
        string basePath,
        bool enableMetricsSnapshotEndpoint)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("TenantOwners")
            .RequireRateLimiting(RateLimitPolicyNames.GeneralRead)
            .WithTags("Observability");

        group.MapGet("/audit-logs", async (
            string? search,
            string? action,
            string? entityName,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            IAuditLogService auditLogService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await auditLogService.ListAsync(search, action, entityName, from, to, page, pageSize, cancellationToken));
        });

        if (enableMetricsSnapshotEndpoint)
        {
            group.MapGet("/metrics", (IMetricsRecorder metricsRecorder) =>
            {
                return Results.Ok(metricsRecorder.Snapshot());
            });

            group.MapGet("/legacy-api-usage", (IMetricsRecorder metricsRecorder) =>
            {
                return Results.Ok(metricsRecorder.LegacyApiUsageReport(DateTimeOffset.UtcNow));
            });
        }

        return app;
    }
}
