using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

using NetCorePal.Extensions.Dto;
using NetCorePal.Extensions.Primitives;

namespace ContentServer.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, message) = exception switch
        {
            KnownException known => (NormalizeStatusCode(known.ErrorCode), known.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "content_or_version_conflict"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "invalid_request"),
            _ => (StatusCodes.Status500InternalServerError, "internal_server_error")
        };

        if (exception is KnownException or BadHttpRequestException)
        {
            logger.LogWarning(exception, "Request failed with a known error: {Message}", message);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ResponseData(false, message, statusCode, null),
            cancellationToken);
        return true;
    }

    private static int NormalizeStatusCode(int errorCode)
    {
        return errorCode is >= 400 and <= 599 ? errorCode : StatusCodes.Status400BadRequest;
    }
}
