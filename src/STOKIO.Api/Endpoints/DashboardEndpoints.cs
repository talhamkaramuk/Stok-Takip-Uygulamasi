using Microsoft.AspNetCore.RateLimiting;
using STOKIO.Api.Security;
using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app, string basePath)
    {
        var group = app.MapGroup(basePath)
            .RequireAuthorization("AuthenticatedStaff")
            .WithTags("Dashboard");

        group.MapGet("/summary", async (IDashboardService dashboardService, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await dashboardService.GetSummaryAsync(cancellationToken));
        })
        .RequireRateLimiting(RateLimitPolicyNames.GeneralRead);

        return app;
    }
}
