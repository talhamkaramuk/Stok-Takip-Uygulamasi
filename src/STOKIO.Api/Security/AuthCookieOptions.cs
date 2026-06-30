using Microsoft.Extensions.Primitives;

namespace STOKIO.Api.Security;

public static class AuthCookieOptions
{
    public const string RefreshTokenCookieName = "stokio.refresh";
    public const string RefreshRequestHeaderName = "X-STOKIO-Refresh";
    public const string RefreshRequestHeaderValue = "1";

    public static CookieOptions CreateRefreshTokenCookieOptions(
        IHostEnvironment environment,
        DateTimeOffset expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt,
            Path = "/api",
            IsEssential = true
        };
    }

    public static CookieOptions CreateExpiredRefreshTokenCookieOptions(IHostEnvironment environment)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UnixEpoch,
            Path = "/api",
            IsEssential = true
        };
    }

    public static bool HasRefreshRequestHeader(HttpRequest request)
    {
        return request.Headers.TryGetValue(RefreshRequestHeaderName, out StringValues values)
            && values.Count == 1
            && string.Equals(values[0], RefreshRequestHeaderValue, StringComparison.Ordinal);
    }
}
