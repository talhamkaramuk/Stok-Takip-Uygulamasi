using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Stock;
using STOKIO.Domain.Enums;

namespace STOKIO.Api.Endpoints;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Stock");

        group.MapPost("/movements", async (
            CreateStockMovementRequest request,
            IValidator<CreateStockMovementRequest> validator,
            IStockService stockService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var movement = await stockService.CreateMovementAsync(request, cancellationToken);
            return Results.Created($"{basePath}/movements/{movement.Id}", movement);
        });

        group.MapGet("/movements", async (
            Guid? productId,
            Guid? warehouseId,
            StockMovementType? type,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            IStockService stockService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await stockService.ListMovementsAsync(productId, warehouseId, type, from, to, page, pageSize, cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapGet("/critical", async (IStockService stockService, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await stockService.ListCriticalStockAsync(cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapGet("/consistency", async (IStockService stockService, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await stockService.CheckConsistencyAsync(cancellationToken));
        })
        .RequireAuthorization("CatalogManagers")
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        return app;
    }
}
