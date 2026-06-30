using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
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
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return validation.ToHttpResult();
            }

            var session = await authService.RegisterTenantAsync(request, cancellationToken);
            WriteRefreshCookie(httpContext, session);
            return Results.Created($"{basePath}/me", session.Response);
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicyNames.AuthRegisterTenant);

        group.MapPost("/login", async (
            LoginRequest request,
            IValidator<LoginRequest> validator,
            AuthRateLimiter authRateLimiter,
            HttpContext httpContext,
            IMetricsRecorder metricsRecorder,
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
                metricsRecorder.RecordLogin(succeeded: false);
                return Results.Problem(
                    type: "https://stokio.local/problems/auth_rate_limited",
                    title: "auth_rate_limited",
                    detail: "Çok fazla giriş denemesi yapıldı. Lütfen daha sonra tekrar deneyin.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            try
            {
                var response = await authService.LoginAsync(request, cancellationToken);
                metricsRecorder.RecordLogin(succeeded: true);
                WriteRefreshCookie(httpContext, response);
                return Results.Ok(response.Response);
            }
            catch (AppProblemException exception) when (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                metricsRecorder.RecordLogin(succeeded: false);
                throw;
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicyNames.AuthLoginIp);

        group.MapPost("/refresh", async (
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            if (!AuthCookieOptions.HasRefreshRequestHeader(httpContext.Request))
            {
                return Results.Problem(
                    type: "https://stokio.local/problems/refresh_csrf_header_missing",
                    title: "refresh_csrf_header_missing",
                    detail: $"Refresh requests must include {AuthCookieOptions.RefreshRequestHeaderName}: {AuthCookieOptions.RefreshRequestHeaderValue}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var session = await authService.RefreshAsync(ReadRefreshToken(httpContext), cancellationToken);
                WriteRefreshCookie(httpContext, session);
                return Results.Ok(session.Response);
            }
            catch (AppProblemException exception) when (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                ClearRefreshCookie(httpContext);
                throw;
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicyNames.AuthLoginIp);

        group.MapPost("/logout", async (
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            if (!AuthCookieOptions.HasRefreshRequestHeader(httpContext.Request))
            {
                return Results.Problem(
                    type: "https://stokio.local/problems/refresh_csrf_header_missing",
                    title: "refresh_csrf_header_missing",
                    detail: $"Logout requests must include {AuthCookieOptions.RefreshRequestHeaderName}: {AuthCookieOptions.RefreshRequestHeaderValue}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await authService.LogoutAsync(ReadRefreshToken(httpContext), cancellationToken);
            ClearRefreshCookie(httpContext);
            return Results.NoContent();
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

    private static string? ReadRefreshToken(HttpContext httpContext)
    {
        return httpContext.Request.Cookies.TryGetValue(AuthCookieOptions.RefreshTokenCookieName, out var value)
            ? value
            : null;
    }

    private static void WriteRefreshCookie(HttpContext httpContext, AuthSession session)
    {
        httpContext.Response.Cookies.Append(
            AuthCookieOptions.RefreshTokenCookieName,
            session.RefreshToken,
            AuthCookieOptions.CreateRefreshTokenCookieOptions(
                httpContext.RequestServices.GetRequiredService<IHostEnvironment>(),
                session.RefreshTokenExpiresAt));
    }

    private static void ClearRefreshCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(
            AuthCookieOptions.RefreshTokenCookieName,
            AuthCookieOptions.CreateExpiredRefreshTokenCookieOptions(
                httpContext.RequestServices.GetRequiredService<IHostEnvironment>()));
    }
}
