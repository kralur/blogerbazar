using Microsoft.AspNetCore.Mvc;
using BloggerBazar.Api.Middleware;

namespace BloggerBazar.Api.Errors;

internal static class ApiProblemWriter
{
    public static async Task WriteAsync(
        HttpContext context,
        int status,
        string? code = null,
        string? title = null,
        string? detail = null,
        object? errors = null)
    {
        var problem = Create(context, status, code, title, detail, errors);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string? code = null,
        string? title = null,
        string? detail = null,
        object? errors = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title ?? GetTitle(status),
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code ?? GetCode(status);
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }

    public static string GetCode(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "invalid_request",
        StatusCodes.Status401Unauthorized => "authentication_required",
        StatusCodes.Status403Forbidden => "access_denied",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status409Conflict => "conflict",
        StatusCodes.Status422UnprocessableEntity => "validation_failed",
        StatusCodes.Status429TooManyRequests => "rate_limited",
        StatusCodes.Status500InternalServerError => "internal_error",
        _ => "request_failed"
    };

    public static string GetTitle(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Invalid request",
        StatusCodes.Status401Unauthorized => "Authentication required",
        StatusCodes.Status403Forbidden => "Access denied",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status409Conflict => "Request conflict",
        StatusCodes.Status422UnprocessableEntity => "Validation failed",
        StatusCodes.Status429TooManyRequests => "Too many requests",
        StatusCodes.Status500InternalServerError => "Internal server error",
        _ => "Request failed"
    };
}
