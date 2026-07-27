using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger, RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Validation failed", exception.Errors.Select(error => new { error.PropertyName, error.ErrorMessage }));
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized", null);
            logger.LogWarning(exception, "Unauthorized request to {Path}", context.Request.Path);
        }
        catch (InvalidOperationException exception)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, exception.Message, null);
            logger.LogInformation(exception, "Business rule conflict on {Path}", context.Request.Path);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception on {Path}", context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected server error occurred.", null);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string title, object? errors)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Extensions = { ["errors"] = errors }
        });
    }
}
