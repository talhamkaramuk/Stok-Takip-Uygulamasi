using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Warehouses;

namespace STOKIO.Api.Endpoints;

public static class WarehouseEndpoints
{
    public static IEndpointRouteBuilder MapWarehouseEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Warehouses");

        group.MapGet("/", async (
            string? search,
            bool? isActive,
            int? page,
            int? pageSize,
            IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await warehouseService.ListAsync(search, isActive, page, pageSize, cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapPost("/", async (
            CreateWarehouseRequest request,
            IValidator<CreateWarehouseRequest> validator,
            IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var warehouse = await warehouseService.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{warehouse.Id}", warehouse);
        })
        .RequireAuthorization("CatalogManagers");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateWarehouseRequest request,
            IValidator<UpdateWarehouseRequest> validator,
            IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await warehouseService.UpdateAsync(id, request, cancellationToken));
        })
        .RequireAuthorization("CatalogManagers");

        group.MapDelete("/{id:guid}", async (Guid id, IWarehouseService warehouseService, CancellationToken cancellationToken) =>
        {
            await warehouseService.DeactivateAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization("CatalogManagers");

        group.MapGet("/stocks", async (
            Guid? warehouseId,
            Guid? productId,
            int? page,
            int? pageSize,
            IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await warehouseService.ListStockAsync(warehouseId, productId, page, pageSize, cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapPost("/transfers", async (
            StockTransferRequest request,
            IValidator<StockTransferRequest> validator,
            IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var transfer = await warehouseService.TransferAsync(request, cancellationToken);
            return Results.Created($"{basePath}/transfers/{transfer.TransferGroupId}", transfer);
        })
        .RequireAuthorization("CatalogManagers");

        return app;
    }
}
