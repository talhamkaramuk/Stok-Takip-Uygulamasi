using FluentValidation;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Counts;

namespace STOKIO.Api.Endpoints;

public static class InventoryCountEndpoints
{
    public static IEndpointRouteBuilder MapInventoryCountEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Inventory Counts");

        group.MapPost("/", async (
            CreateInventoryCountRequest request,
            IValidator<CreateInventoryCountRequest> validator,
            IInventoryCountService countService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var count = await countService.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{count.Id}", count);
        });

        group.MapGet("/{id:guid}", async (Guid id, IInventoryCountService countService, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await countService.GetAsync(id, cancellationToken));
        });

        group.MapPost("/{id:guid}/items/scan", async (
            Guid id,
            ScanCountItemRequest request,
            IValidator<ScanCountItemRequest> validator,
            IInventoryCountService countService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await countService.ScanAsync(id, request, cancellationToken));
        });

        group.MapPost("/{id:guid}/close", async (
            Guid id,
            CloseInventoryCountRequest request,
            IInventoryCountService countService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await countService.CloseAsync(id, request, cancellationToken));
        });

        group.MapGet("/{id:guid}/differences", async (
            Guid id,
            IInventoryCountService countService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await countService.GetDifferencesAsync(id, cancellationToken));
        });

        return app;
    }
}
