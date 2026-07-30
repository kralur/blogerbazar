using FluentValidation;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Api.Errors;
using System.Security.Authentication;

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
            var errors = exception.Errors.GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status422UnprocessableEntity, "validation_failed", detail: "One or more validation errors occurred.", errors: errors);
        }
        catch (BadHttpRequestException)
        {
            if (context.Request.Path.StartsWithSegments("/api/webhooks/telegram"))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.CompleteAsync();
                return;
            }

            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status400BadRequest);
        }
        catch (AuthenticationException)
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status401Unauthorized);
            logger.LogWarning("Authentication rejected for {Path}", context.Request.Path);
        }
        catch (UnauthorizedAccessException)
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status403Forbidden);
            logger.LogWarning("Authorization rejected for {Path}", context.Request.Path);
        }
        catch (InvalidOperationException exception)
        {
            var isNotFound = exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("has not registered", StringComparison.OrdinalIgnoreCase);
            await ApiProblemWriter.WriteAsync(context, isNotFound ? StatusCodes.Status404NotFound : StatusCodes.Status409Conflict);
            logger.LogInformation("Business request rejected for {Path}; Outcome {Outcome}", context.Request.Path, isNotFound ? "not_found" : "conflict");
        }
        catch (PaymentProviderUnavailableException)
        {
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "payment_provider_unavailable", "Payment provider unavailable");
            logger.LogWarning("Payment provider unavailable for {Path}", context.Request.Path);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception on {Path}", context.Request.Path);
            await ApiProblemWriter.WriteAsync(context, StatusCodes.Status500InternalServerError);
        }
    }
}
