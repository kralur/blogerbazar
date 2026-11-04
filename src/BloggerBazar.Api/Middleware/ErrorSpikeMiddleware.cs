using BloggerBazar.Application.Notifications;

namespace BloggerBazar.Api.Middleware;

// D59: sits outside the exception handler and the rate limiter, so it sees every final status (500 from an unhandled
// error, 429 from the limiter). The alert goes out in the background: the request never waits for Telegram.
public sealed class ErrorSpikeMiddleware(RequestDelegate next, ErrorSpikeMonitor monitor, IServiceScopeFactory scopes, ILogger<ErrorSpikeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        finally
        {
            var status = context.Response.StatusCode;
            if ((status >= 500 || status == StatusCodes.Status429TooManyRequests) && monitor.Record(status) is { } alert)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<ErrorSpikeAlertSender>().SendAsync(alert, CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        logger.LogWarning(exception, "Error-spike alert could not be sent");
                    }
                });
            }
        }
    }
}
