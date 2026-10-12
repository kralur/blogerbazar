namespace BloggerBazar.Api.Middleware;

// Security 3: headers every API response carries, error responses included. The API returns only JSON,
// so it is never shown in a frame, never sniffed as another type and never kept by shared caches.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var headers = ((HttpContext)state).Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Resource-Policy"] = "same-site";
            // Answers carry personal data (phones, deals); a proxy or the device cache must not keep them.
            if (string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        }, context);
        return next(context);
    }
}
