using Serilog.Context;
using System.Diagnostics;

namespace BloggerBazar.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const string RequestIdHeader = "X-Request-ID";
    public const string CorrelationIdItemKey = "correlation-id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.Response.Headers[RequestIdHeader] = context.TraceIdentifier;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
        {
            await next(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var requested = context.Request.Headers[CorrelationIdHeader].ToString();
        if (requested.Length is > 0 and <= 128 && requested.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'))
        {
            return requested;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }
}
