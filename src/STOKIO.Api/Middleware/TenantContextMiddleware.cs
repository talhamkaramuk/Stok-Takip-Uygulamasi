using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, ICurrentTenant currentTenant)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdValue = httpContext.User.FindFirst("tenant_id")?.Value;
            var tenantSlug = httpContext.User.FindFirst("tenant_slug")?.Value;
            if (Guid.TryParse(tenantIdValue, out var tenantId))
            {
                currentTenant.SetTenant(tenantId, tenantSlug);
            }
        }

        await next(httpContext);
    }
}

