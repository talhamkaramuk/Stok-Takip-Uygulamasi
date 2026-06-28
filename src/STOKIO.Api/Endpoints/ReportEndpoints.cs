using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .RequireRateLimiting(RateLimitPolicyNames.Report)
            .WithTags("Reports");

        group.MapGet("/current-stock", async (IReportService reportService, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await reportService.CurrentStockAsync(cancellationToken));
        });

        group.MapGet("/critical-stock", async (IStockService stockService, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await stockService.ListCriticalStockAsync(cancellationToken));
        });

        group.MapGet("/movements", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await reportService.MovementsAsync(from, to, cancellationToken));
        });

        group.MapGet("/count-differences/{countId:guid}", async (
            Guid countId,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await reportService.CountDifferencesAsync(countId, cancellationToken));
        });

        return app;
    }
}
