using BloggerBazar.Api.Errors;
using BloggerBazar.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BloggerBazar.Api.Filters;

public sealed class ProblemDetailsEnrichmentFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/webhooks/telegram"))
        {
            await next();
            return;
        }

        if (context.Result is ObjectResult { Value: ProblemDetails problem })
        {
            Enrich(context.HttpContext, problem, problem.Status ?? StatusCodes.Status500InternalServerError);
        }
        else if (context.Result is StatusCodeResult { StatusCode: >= 400 } result)
        {
            context.Result = new ObjectResult(ApiProblemWriter.Create(context.HttpContext, result.StatusCode))
            {
                StatusCode = result.StatusCode,
                ContentTypes = { "application/problem+json" }
            };
        }

        await next();
    }

    private static void Enrich(HttpContext context, ProblemDetails problem, int status)
    {
        problem.Status = status;
        problem.Title = ApiProblemWriter.GetTitle(status);
        problem.Detail = problem is ValidationProblemDetails
            ? "One or more validation errors occurred."
            : null;
        problem.Instance = context.Request.Path;
        problem.Extensions["code"] = problem is ValidationProblemDetails
            ? "validation_failed"
            : ApiProblemWriter.GetCode(status);
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }
    }
}
