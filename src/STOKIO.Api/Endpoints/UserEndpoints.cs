using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Users;

namespace STOKIO.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("TenantOwners")
            .WithTags("Users");

        group.MapGet("/", async (
            string? search,
            bool? isActive,
            int? page,
            int? pageSize,
            IUserManagementService userService,
            CancellationToken cancellationToken) =>
        {
            return Results.Ok(await userService.ListAsync(search, isActive, page, pageSize, cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        group.MapPost("/", async (
            CreateUserRequest request,
            IValidator<CreateUserRequest> validator,
            IUserManagementService userService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var user = await userService.CreateAsync(request, cancellationToken);
            return Results.Created($"{basePath}/{user.Id}", user);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserRequest request,
            IValidator<UpdateUserRequest> validator,
            IUserManagementService userService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await userService.UpdateAsync(id, request, cancellationToken));
        });

        group.MapDelete("/{id:guid}", async (Guid id, IUserManagementService userService, CancellationToken cancellationToken) =>
        {
            await userService.DeactivateAsync(id, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
