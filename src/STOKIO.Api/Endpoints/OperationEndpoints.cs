using FluentValidation;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Operations;
using STOKIO.Domain.Enums;

namespace STOKIO.Api.Endpoints;

public static class OperationEndpoints
{
    public static IEndpointRouteBuilder MapSalesOrderEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Sales Orders");

        group.MapGet("/", async (
            SalesOrderStatus? status,
            int? page,
            int? pageSize,
            ISalesOrderService service,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await service.ListAsync(status, page, pageSize, cancellationToken));
        });

        group.MapPost("/", async (
            CreateSalesOrderRequest request,
            IValidator<CreateSalesOrderRequest> validator,
            ISalesOrderService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var order = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{order.Id}", order);
        });

        return app;
    }

    public static IEndpointRouteBuilder MapPurchaseRequestEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Purchase Requests");

        group.MapGet("/", async (
            PurchaseRequestStatus? status,
            int? page,
            int? pageSize,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await service.ListAsync(status, page, pageSize, cancellationToken));
        });

        group.MapPost("/", async (
            CreatePurchaseRequestRequest request,
            IValidator<CreatePurchaseRequestRequest> validator,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var purchaseRequest = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{purchaseRequest.Id}", purchaseRequest);
        });

        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await service.ApproveAsync(id, cancellationToken));
        })
        .RequireAuthorization("CatalogManagers");

        group.MapPost("/{id:guid}/receive", async (
            Guid id,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await service.ReceiveAsync(id, cancellationToken));
        })
        .RequireAuthorization("CatalogManagers");

        return app;
    }

    public static IEndpointRouteBuilder MapShipmentEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Shipments");

        group.MapGet("/", async (
            ShipmentStatus? status,
            int? page,
            int? pageSize,
            IShipmentService service,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await service.ListAsync(status, page, pageSize, cancellationToken));
        });

        group.MapPost("/", async (
            CreateShipmentRequest request,
            IValidator<CreateShipmentRequest> validator,
            IShipmentService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var shipment = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{shipment.Id}", shipment);
        })
        .RequireAuthorization("CatalogManagers");

        return app;
    }

    public static IEndpointRouteBuilder MapReturnRequestEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Returns");

        group.MapGet("/", async (
            ReturnRequestStatus? status,
            int? page,
            int? pageSize,
            IReturnRequestService service,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await service.ListAsync(status, page, pageSize, cancellationToken));
        });

        group.MapPost("/", async (
            CreateReturnRequestRequest request,
            IValidator<CreateReturnRequestRequest> validator,
            IReturnRequestService service,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var returnRequest = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{returnRequest.Id}", returnRequest);
        })
        .RequireAuthorization("CatalogManagers");

        return app;
    }
}
