using Microsoft.AspNetCore.Diagnostics;

namespace STI.City.API.Errors;

public sealed class SanitizedExceptionHandler(
    ILogger<SanitizedExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled request failure for {RequestPath}",
            httpContext.Request.Path);

        httpContext.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        await Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Internal server error",
                extensions: new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier
                })
            .ExecuteAsync(httpContext);

        return true;
    }
}
