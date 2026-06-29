using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;

namespace STOKIO.Api.Middleware;

public sealed class ProblemDetailsMiddleware(
    RequestDelegate next,
    ILogger<ProblemDetailsMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppProblemException exception)
        {
            await WriteProblemAsync(context, exception.StatusCode, exception.Code, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogWarning(exception, "Unauthorized data access was blocked.");
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "forbidden", "The operation is not allowed.");
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Concurrent stock update was rejected.");
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "stock_concurrency_conflict", "Stock changed while this operation was being processed. Please retry.");
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Database update failed.");
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "database_conflict", "The requested change conflicts with existing data.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API error.");
            var detail = environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.";
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "server_error", detail);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string code, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = code,
            Detail = detail,
            Type = $"https://stokio.local/problems/{code}"
        };
        var correlationId = context.RequestServices.GetRequiredService<ICorrelationContext>().CorrelationId;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        await context.Response.WriteAsJsonAsync(problem);
    }
}
