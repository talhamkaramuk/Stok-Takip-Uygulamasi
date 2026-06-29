using Serilog.Context;
using STOKIO.Application.Abstractions;

namespace STOKIO.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxCorrelationIdLength = 100;

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var correlationId = ResolveCorrelationId(context);
        correlationContext.SetCorrelationId(correlationId);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var candidate = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return Guid.CreateVersion7().ToString("N");
        }

        candidate = candidate.Trim();
        return candidate.Length <= MaxCorrelationIdLength
            ? candidate
            : candidate[..MaxCorrelationIdLength];
    }
}
