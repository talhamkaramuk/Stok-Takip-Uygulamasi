using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Exports;

namespace STOKIO.Api.Endpoints;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .RequireRateLimiting(RateLimitPolicyNames.Export)
            .WithTags("Exports");

        group.MapPost("/jobs", async (
            CreateExportJobRequest request,
            IExportJobService exportJobService,
            CancellationToken cancellationToken) =>
        {
            var job = await exportJobService.CreateAsync(request, cancellationToken);
            return Results.Accepted($"{basePath}/jobs/{job.Id}", job);
        });

        group.MapGet("/jobs/{jobId:guid}", async (
            Guid jobId,
            IExportJobService exportJobService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await exportJobService.GetAsync(jobId, cancellationToken));
        });

        group.MapGet("/jobs/{jobId:guid}/download", async (
            Guid jobId,
            IExportJobService exportJobService,
            CancellationToken cancellationToken) =>
        {
            return ToFileResult(await exportJobService.DownloadAsync(jobId, cancellationToken));
        });

        group.MapGet("/current-stock.xlsx", async (IExportService exportService, CancellationToken cancellationToken) =>
        {
            return ToFileResult(await exportService.CurrentStockAsync(cancellationToken));
        });

        group.MapGet("/critical-stock.xlsx", async (IExportService exportService, CancellationToken cancellationToken) =>
        {
            return ToFileResult(await exportService.CriticalStockAsync(cancellationToken));
        });

        group.MapGet("/movements.xlsx", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            IExportService exportService,
            CancellationToken cancellationToken) =>
        {
            return ToFileResult(await exportService.MovementsAsync(from, to, cancellationToken));
        });

        group.MapGet("/count-differences/{countId:guid}.xlsx", async (
            Guid countId,
            IExportService exportService,
            CancellationToken cancellationToken) =>
        {
            return ToFileResult(await exportService.CountDifferencesAsync(countId, cancellationToken));
        });

        return app;
    }

    private static IResult ToFileResult(ExportFile file)
    {
        return Results.File(file.Content, file.ContentType, file.FileName);
    }
}
