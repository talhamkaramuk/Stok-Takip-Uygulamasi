using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Net.Http.Headers;
using STOKIO.Application.Dtos.Auth;

namespace STOKIO.Api.Security;

public sealed class AuthRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _loginLimiter =
        PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(
                key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(5),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    public async ValueTask<bool> TryAcquireLoginAsync(
        HttpContext context,
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        using var lease = await _loginLimiter.AcquireAsync(
            RateLimitPartitionKeys.ForLogin(context, request.TenantSlug, request.Email),
            permitCount: 1,
            cancellationToken);

        if (lease.IsAcquired)
        {
            return true;
        }

        RateLimitHeaders.TrySetRetryAfter(context, lease);
        return false;
    }

    public void Dispose()
    {
        _loginLimiter.Dispose();
    }
}

public static class RateLimitPolicyNames
{
    public const string AuthLoginIp = "auth-login-ip";
    public const string AuthRegisterTenant = "auth-register-tenant";
    public const string BarcodeScan = "barcode-scan";
    public const string Export = "export";
    public const string Report = "report";
    public const string GeneralRead = "general-read";
}

public static class RateLimitPartitionKeys
{
    private const int MaxPartitionValueLength = 160;

    public static string ForLogin(HttpContext context, string tenantSlug, string email)
    {
        return HashKey("login", RemoteIp(context), Normalize(tenantSlug), Normalize(email));
    }

    public static string ForIp(HttpContext context, string scope)
    {
        return HashKey(scope, RemoteIp(context));
    }

    public static string ForTenantOrIp(HttpContext context, string scope)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(tenantId))
        {
            return HashKey(scope, "tenant", Normalize(tenantId));
        }

        return HashKey(scope, "ip", RemoteIp(context));
    }

    public static string ForTenantUserOrIp(HttpContext context, string scope)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(tenantId))
        {
            return HashKey(scope, "tenant-user", Normalize(tenantId), Normalize(userId ?? "unknown"));
        }

        return HashKey(scope, "ip", RemoteIp(context));
    }

    private static string RemoteIp(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string Normalize(string value)
    {
        value = value.Trim().ToLowerInvariant();
        return value.Length <= MaxPartitionValueLength ? value : value[..MaxPartitionValueLength];
    }

    private static string HashKey(string scope, params string[] values)
    {
        var raw = string.Join('|', values.Select(Normalize));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"{scope}:{Convert.ToHexString(hash)}";
    }
}

public static class RateLimitHeaders
{
    public static void TrySetRetryAfter(HttpContext context, RateLimitLease lease)
    {
        if (!lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            return;
        }

        context.Response.Headers[HeaderNames.RetryAfter] =
            Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
    }
}
