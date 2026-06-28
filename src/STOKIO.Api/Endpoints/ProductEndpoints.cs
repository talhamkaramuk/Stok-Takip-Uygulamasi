using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Products;

namespace STOKIO.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Products");

        group.MapGet("/", async (
            string? search,
            Guid? categoryId,
            bool? isActive,
            int? page,
            int? pageSize,
            IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var products = await productService.ListAsync(search, categoryId, isActive, page, pageSize, cancellationToken);
            return Results.Ok(products);
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapGet("/{id:guid}", async (Guid id, IProductService productService, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await productService.GetAsync(id, cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapPost("/", async (
            CreateProductRequest request,
            IValidator<CreateProductRequest> validator,
            IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var product = await productService.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{product.Id}", product);
        })
        .RequireAuthorization("CatalogManagers");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProductRequest request,
            IValidator<UpdateProductRequest> validator,
            IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await productService.UpdateAsync(id, request, cancellationToken));
        })
        .RequireAuthorization("CatalogManagers");

        group.MapPost("/{id:guid}/barcodes", async (
            Guid id,
            AddBarcodeRequest request,
            IValidator<AddBarcodeRequest> validator,
            IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await productService.AddBarcodeAsync(id, request, cancellationToken));
        })
        .RequireAuthorization("CatalogManagers");

        group.MapDelete("/{id:guid}", async (Guid id, IProductService productService, CancellationToken cancellationToken) =>
        {
            await productService.DeactivateAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization("CatalogManagers");

        return app;
    }
}
