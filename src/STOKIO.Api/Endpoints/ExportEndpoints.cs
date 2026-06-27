using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Endpoints;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Exports");

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

    private static IResult ToFileResult(STOKIO.Application.Dtos.Exports.ExportFile file)
    {
        return Results.File(file.Content, file.ContentType, file.FileName);
    }
}
