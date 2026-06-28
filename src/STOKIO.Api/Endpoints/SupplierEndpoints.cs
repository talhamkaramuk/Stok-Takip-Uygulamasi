using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Parties;

namespace STOKIO.Api.Endpoints;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Suppliers");

        group.MapGet("/", async (
            string? search,
            bool? isActive,
            int? page,
            int? pageSize,
            ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await supplierService.ListAsync(search, isActive, page, pageSize, cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapPost("/", async (
            CreateSupplierRequest request,
            IValidator<CreateSupplierRequest> validator,
            ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var supplier = await supplierService.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{supplier.Id}", supplier);
        })
        .RequireAuthorization("CatalogManagers");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSupplierRequest request,
            IValidator<UpdateSupplierRequest> validator,
            ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await supplierService.UpdateAsync(id, request, cancellationToken));
        })
        .RequireAuthorization("CatalogManagers");

        group.MapDelete("/{id:guid}", async (Guid id, ISupplierService supplierService, CancellationToken cancellationToken) =>
        {
            await supplierService.DeactivateAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization("CatalogManagers");

        return app;
    }
}
