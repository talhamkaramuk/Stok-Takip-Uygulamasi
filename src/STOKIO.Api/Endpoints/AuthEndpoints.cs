using FluentValidation;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Dtos.Auth;

namespace STOKIO.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .WithTags("Authentication");

        group.MapPost("/register-tenant", async (
            RegisterTenantRequest request,
            IValidator<RegisterTenantRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var response = await authService.RegisterTenantAsync(request, cancellationToken);
            return Results.Created($"{basePath}/me", response);
        })
        .AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequest request,
            IValidator<LoginRequest> validator,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            return Results.Ok(await authService.LoginAsync(request, cancellationToken));
        })
        .AllowAnonymous();

        group.MapGet("/me", (ICurrentTenant currentTenant, ICurrentUser currentUser) =>
        {
            return Results.Ok(new
            {
                tenantId = currentTenant.TenantId,
                tenantSlug = currentTenant.TenantSlug,
                userId = currentUser.UserId,
                email = currentUser.Email,
                role = currentUser.Role
            });
        })
        .RequireAuthorization();

        return app;
    }
}
