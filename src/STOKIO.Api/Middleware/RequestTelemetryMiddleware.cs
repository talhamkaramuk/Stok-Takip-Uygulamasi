using Serilog.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;

namespace STOKIO.Api.Middleware;

public sealed class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IMetricsRecorder metricsRecorder)
    {
        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        var statusCode = StatusCodes.Status500InternalServerError;
        var route = context.Request.Path.Value ?? string.Empty;

        using (LogContext.PushProperty("TenantId", currentTenant.HasTenant ? currentTenant.TenantId : null))
        using (LogContext.PushProperty("UserId", currentUser.UserId))
        using (LogContext.PushProperty("Route", route))
        {
            try
            {
                await next(context);
                statusCode = context.Response.StatusCode;
            }
            catch (Exception exception)
            {
                statusCode = context.Response.HasStarted
                    ? context.Response.StatusCode
                    : StatusCodeFor(exception);

                throw;
            }
            finally
            {
                var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                metricsRecorder.RecordRequest(statusCode, elapsedMs);
                if (LegacyApiDeprecationMiddleware.IsLegacyApiRequest(context.Request.Path))
                {
                    metricsRecorder.RecordLegacyApiRequest(
                        context.Request.Method,
                        RouteForMetrics(context),
                        ClientForReport(context.Request),
                        DateTimeOffset.UtcNow);
                }

                logger.LogInformation(
                    "HTTP {Method} {Route} responded {StatusCode} in {ElapsedMs:0.0} ms.",
                    context.Request.Method,
                    route,
                    statusCode,
                    elapsedMs);
            }
        }
    }

    private static int StatusCodeFor(Exception exception)
    {
        return exception switch
        {
            AppProblemException problem => problem.StatusCode,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            DbUpdateException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static string RouteForMetrics(HttpContext context)
    {
        if (context.GetEndpoint() is RouteEndpoint routeEndpoint
            && !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText))
        {
            return routeEndpoint.RoutePattern.RawText;
        }

        return "unmatched_legacy_api";
    }

    private static string ClientForReport(HttpRequest request)
    {
        var explicitClient = request.Headers["X-STOKIO-Client"].ToString();
        if (!string.IsNullOrWhiteSpace(explicitClient))
        {
            return explicitClient;
        }

        var clientId = request.Headers["X-Client-Id"].ToString();
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            return clientId;
        }

        var userAgent = request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? "unknown" : userAgent;
    }
}
