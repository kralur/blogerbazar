namespace BloggerBazar.Infrastructure.Security;

// D64 stage B: business work in a browser on the Mini App's own address (Telegram:MiniAppUrl). Nothing has to be
// configured: the defaults below are the agreed ones, and without a valid Mini App address browser sign-in stays off.
public sealed class BrowserSessionOptions
{
    public const string SectionName = "BrowserSessions";

    // A session ends this many hours after sign-in, however active it is.
    public int SessionHours { get; init; } = 12;
    // ...and earlier after this many minutes without a request.
    public int IdleMinutes { get; init; } = 120;
    // A sign-in code is valid this long.
    public int SignInSeconds { get; init; } = 120;

    // "scheme://host[:port]" of the Mini App in lowercase, as browsers send it in the Origin header; null when the
    // address is missing or not https (plain http only on localhost). The path of the Mini App address is ignored.
    public static string? OriginOf(string? miniAppUrl)
    {
        if (string.IsNullOrWhiteSpace(miniAppUrl) || !Uri.TryCreate(miniAppUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var secure = uri.Scheme == Uri.UriSchemeHttps;
        var localHttp = uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        if (!(secure || localHttp) || uri.UserInfo.Length > 0)
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }
}
