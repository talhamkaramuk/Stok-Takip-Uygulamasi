using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
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
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicyNames.AuthRegisterTenant);

        group.MapPost("/login", async (
            LoginRequest request,
            IValidator<LoginRequest> validator,
            AuthRateLimiter authRateLimiter,
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            if (!await authRateLimiter.TryAcquireLoginAsync(httpContext, request, cancellationToken))
            {
                return Results.Problem(
                    type: "https://stokio.local/problems/auth_rate_limited",
                    title: "auth_rate_limited",
                    detail: "Too many login attempts. Please try again later.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            return Results.Ok(await authService.LoginAsync(request, cancellationToken));
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicyNames.AuthLoginIp);

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
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        return app;
    }
}
