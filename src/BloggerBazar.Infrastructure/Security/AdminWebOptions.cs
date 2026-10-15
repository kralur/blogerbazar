namespace BloggerBazar.Infrastructure.Security;

// The web admin (admin-web): sign-in with the Telegram Login Widget, then a server-side session in an HttpOnly cookie.
// Without a valid Origin the web sign-in stays switched off and the API behaves exactly as before.
public sealed class AdminWebOptions
{
    public const string SectionName = "AdminWeb";

    // The exact address of the admin site, for example https://admin-web-production.up.railway.app.
    public string Origin { get; init; } = string.Empty;
    // A session ends this many hours after sign-in, however active it is.
    public int SessionHours { get; init; } = 8;
    // ...and earlier after this many minutes without a request.
    public int IdleMinutes { get; init; } = 60;
    // Signed widget data older than this is refused.
    public int MaxAuthAgeSeconds { get; init; } = 300;

    public string? NormalizedOrigin => NormalizeOrigin(Origin);

    // "scheme://host[:port]" in lowercase, as browsers send it in the Origin header; null for anything else
    // (a path, a query, credentials, plain http outside localhost).
    public static string? NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var secure = uri.Scheme == Uri.UriSchemeHttps;
        var localHttp = uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        if (!(secure || localHttp) || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }
}
