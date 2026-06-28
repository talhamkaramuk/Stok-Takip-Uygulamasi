using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace STOKIO.Api.Security;

public static class RateLimitServiceCollectionExtensions
{
    public static IServiceCollection AddStokioRateLimiting(this IServiceCollection services)
    {
        services.AddSingleton<AuthRateLimiter>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                RateLimitHeaders.TrySetRetryAfter(context.HttpContext, context.Lease);
                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitPartitionKeys.ForTenantOrIp(context, "global"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 600,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(RateLimitPolicyNames.AuthLoginIp, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitPartitionKeys.ForIp(context, RateLimitPolicyNames.AuthLoginIp),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(RateLimitPolicyNames.AuthRegisterTenant, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitPartitionKeys.ForIp(context, RateLimitPolicyNames.AuthRegisterTenant),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(RateLimitPolicyNames.BarcodeScan, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    RateLimitPartitionKeys.ForTenantUserOrIp(context, RateLimitPolicyNames.BarcodeScan),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 60,
                        TokensPerPeriod = 30,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(RateLimitPolicyNames.Export, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitPartitionKeys.ForTenantUserOrIp(context, RateLimitPolicyNames.Export),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(RateLimitPolicyNames.Report, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitPartitionKeys.ForTenantOrIp(context, RateLimitPolicyNames.Report),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(RateLimitPolicyNames.GeneralRead, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RateLimitPartitionKeys.ForTenantOrIp(context, RateLimitPolicyNames.GeneralRead),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }
}
