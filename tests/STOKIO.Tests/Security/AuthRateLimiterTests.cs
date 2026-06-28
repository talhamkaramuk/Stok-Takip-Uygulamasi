using System.Net;
using Microsoft.AspNetCore.Http;
using STOKIO.Api.Security;
using STOKIO.Application.Dtos.Auth;

namespace STOKIO.Tests.Security;

public sealed class AuthRateLimiterTests
{
    [Fact]
    public async Task TryAcquireLoginAsync_BlocksSameIpTenantAndEmail_WhenLimitIsExceeded()
    {
        using var limiter = new AuthRateLimiter();
        var context = CreateContext();
        var request = new LoginRequest("stokio-demo", "owner@stokio.local", "password");

        for (var i = 0; i < 5; i++)
        {
            Assert.True(await limiter.TryAcquireLoginAsync(context, request, CancellationToken.None));
        }

        Assert.False(await limiter.TryAcquireLoginAsync(context, request, CancellationToken.None));
        Assert.True(context.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task TryAcquireLoginAsync_UsesDifferentPartition_ForDifferentEmail()
    {
        using var limiter = new AuthRateLimiter();
        var context = CreateContext();
        var request = new LoginRequest("stokio-demo", "owner@stokio.local", "password");

        for (var i = 0; i < 5; i++)
        {
            Assert.True(await limiter.TryAcquireLoginAsync(context, request, CancellationToken.None));
        }

        Assert.True(await limiter.TryAcquireLoginAsync(
            context,
            new LoginRequest("stokio-demo", "manager@stokio.local", "password"),
            CancellationToken.None));
    }

    [Fact]
    public async Task TryAcquireLoginAsync_NormalizesTenantAndEmailPartition()
    {
        using var limiter = new AuthRateLimiter();
        var context = CreateContext();

        for (var i = 0; i < 5; i++)
        {
            Assert.True(await limiter.TryAcquireLoginAsync(
                context,
                new LoginRequest(" stokio-demo ", " OWNER@STOKIO.LOCAL ", "password"),
                CancellationToken.None));
        }

        Assert.False(await limiter.TryAcquireLoginAsync(
            context,
            new LoginRequest("STOKIO-DEMO", "owner@stokio.local", "password"),
            CancellationToken.None));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        return context;
    }
}
