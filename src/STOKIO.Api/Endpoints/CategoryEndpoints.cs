using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Categories;

namespace STOKIO.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Categories");

        group.MapGet("/", async (
            string? search,
            bool? isActive,
            int? page,
            int? pageSize,
            ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await categoryService.ListAsync(search, isActive, page, pageSize, cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapPost("/", async (
            CreateCategoryRequest request,
            IValidator<CreateCategoryRequest> validator,
            ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var category = await categoryService.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{category.Id}", category);
        })
        .RequireAuthorization("CatalogManagers");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCategoryRequest request,
            IValidator<UpdateCategoryRequest> validator,
            ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await categoryService.UpdateAsync(id, request, cancellationToken));
        })
        .RequireAuthorization("CatalogManagers");

        group.MapDelete("/{id:guid}", async (Guid id, ICategoryService categoryService, CancellationToken cancellationToken) =>
        {
            await categoryService.DeactivateAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization("CatalogManagers");

        return app;
    }
}
