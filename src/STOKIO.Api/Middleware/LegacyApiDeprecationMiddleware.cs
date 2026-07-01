using Microsoft.AspNetCore.Http;

namespace STOKIO.Api.Middleware;

public sealed class LegacyApiDeprecationMiddleware(RequestDelegate next)
{
    public const string DeprecationHeaderName = "Deprecation";
    public const string DeprecationHeaderValue = "true";
    public const string SunsetHeaderName = "Sunset";
    public const string SunsetHeaderValue = "Thu, 31 Dec 2026 23:59:59 GMT";
    public const string LinkHeaderName = "Link";
    public const string LinkHeaderValue = "</api/v1>; rel=\"successor-version\"";
    public static readonly DateTimeOffset SunsetAtUtc = new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
    public static readonly DateTimeOffset RemoveMappingsAfterUtc = SunsetAtUtc;

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsLegacyApiRequest(context.Request.Path))
        {
            context.Response.Headers[DeprecationHeaderName] = DeprecationHeaderValue;
            context.Response.Headers[SunsetHeaderName] = SunsetHeaderValue;
            context.Response.Headers[LinkHeaderName] = LinkHeaderValue;
        }

        await next(context);
    }

    internal static bool IsLegacyApiRequest(PathString path)
    {
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldMapLegacyRoutes(DateTimeOffset nowUtc)
    {
        return nowUtc <= RemoveMappingsAfterUtc;
    }
}
