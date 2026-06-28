using Microsoft.AspNetCore.Http;
using STOKIO.Api.Middleware;

namespace STOKIO.Tests.Middleware;

public sealed class LegacyApiDeprecationMiddlewareTests
{
    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/auth/login")]
    [InlineData("/API/reports")]
    public async Task InvokeAsync_AddsDeprecationHeaders_ForLegacyApiRoutes(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new LegacyApiDeprecationMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(LegacyApiDeprecationMiddleware.DeprecationHeaderValue, context.Response.Headers[LegacyApiDeprecationMiddleware.DeprecationHeaderName]);
        Assert.Equal(LegacyApiDeprecationMiddleware.SunsetHeaderValue, context.Response.Headers[LegacyApiDeprecationMiddleware.SunsetHeaderName]);
        Assert.Equal(LegacyApiDeprecationMiddleware.LinkHeaderValue, context.Response.Headers[LegacyApiDeprecationMiddleware.LinkHeaderName]);
    }

    [Theory]
    [InlineData("/api/v1/products")]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/health")]
    [InlineData("/apiary/products")]
    public async Task InvokeAsync_DoesNotAddDeprecationHeaders_ForCurrentOrNonApiRoutes(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new LegacyApiDeprecationMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey(LegacyApiDeprecationMiddleware.DeprecationHeaderName));
        Assert.False(context.Response.Headers.ContainsKey(LegacyApiDeprecationMiddleware.SunsetHeaderName));
        Assert.False(context.Response.Headers.ContainsKey(LegacyApiDeprecationMiddleware.LinkHeaderName));
    }
}
