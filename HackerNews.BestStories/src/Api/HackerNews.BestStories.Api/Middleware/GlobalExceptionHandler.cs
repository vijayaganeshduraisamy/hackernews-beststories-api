using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.BestStories.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger): IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An unhandled exception occurred.");

        var (statusCode, title) = exception switch
        {
            ArgumentOutOfRangeException =>
            (StatusCodes.Status400BadRequest, "Invalid request"),

            ArgumentException =>
            (StatusCodes.Status400BadRequest, "Invalid request"),

            TimeoutException =>
            (StatusCodes.Status503ServiceUnavailable, "Service unavailable"),

            _ =>
            (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
        problemDetails,
        cancellationToken);

        return true;
    }
}
