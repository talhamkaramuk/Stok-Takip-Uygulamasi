using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using STOKIO.Api.Security;

namespace STOKIO.Tests.Security;

public sealed class AuthCookieOptionsTests
{
    [Fact]
    public void CreateRefreshTokenCookieOptions_UsesHttpOnlySecureAndSameSiteOutsideDevelopment()
    {
        var expiresAt = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

        var options = AuthCookieOptions.CreateRefreshTokenCookieOptions(
            new TestHostEnvironment(Environments.Production),
            expiresAt);

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
        Assert.Equal("/api", options.Path);
        Assert.Equal(expiresAt, options.Expires);
    }

    [Fact]
    public void HasRefreshRequestHeader_RequiresExactCsrfHeader()
    {
        var context = new DefaultHttpContext();
        Assert.False(AuthCookieOptions.HasRefreshRequestHeader(context.Request));

        context.Request.Headers[AuthCookieOptions.RefreshRequestHeaderName] = AuthCookieOptions.RefreshRequestHeaderValue;
        Assert.True(AuthCookieOptions.HasRefreshRequestHeader(context.Request));

        context.Request.Headers[AuthCookieOptions.RefreshRequestHeaderName] = "wrong";
        Assert.False(AuthCookieOptions.HasRefreshRequestHeader(context.Request));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "STOKIO.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
