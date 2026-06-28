using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Parties;

namespace STOKIO.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Customers");

        group.MapGet("/", async (
            string? search,
            bool? isActive,
            int? page,
            int? pageSize,
            ICustomerService customerService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await customerService.ListAsync(search, isActive, page, pageSize, cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapPost("/", async (
            CreateCustomerRequest request,
            IValidator<CreateCustomerRequest> validator,
            ICustomerService customerService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var customer = await customerService.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{customer.Id}", customer);
        })
        .RequireAuthorization("CatalogManagers");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCustomerRequest request,
            IValidator<UpdateCustomerRequest> validator,
            ICustomerService customerService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await customerService.UpdateAsync(id, request, cancellationToken));
        })
        .RequireAuthorization("CatalogManagers");

        group.MapDelete("/{id:guid}", async (Guid id, ICustomerService customerService, CancellationToken cancellationToken) =>
        {
            await customerService.DeactivateAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization("CatalogManagers");

        return app;
    }
}
