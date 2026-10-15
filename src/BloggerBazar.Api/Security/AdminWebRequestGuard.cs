using System.Security.Authentication;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.AdminWeb;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace BloggerBazar.Api.Security;

// The web admin's session cookie. "__Host-": the browser keeps it only for this exact host, over HTTPS, for the whole
// site; HttpOnly keeps it away from page scripts; SameSite=Strict keeps other sites from sending it.
internal static class AdminWebCookie
{
    public const string Name = "__Host-bb_admin";

    public static CookieOptions Create(DateTime expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero)
    };

    public static CookieOptions Expired() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true
    };

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var token) && !string.IsNullOrEmpty(token) ? token : null;
}

// Marks the web admin sign-in action: the "api" rate limit policy gives it its own small per-address budget.
// Endpoint metadata rather than a path match, so no spelling of the URL (trailing slash, /api/v1) escapes it.
[AttributeUsage(AttributeTargets.Method)]
public sealed class AdminWebSignInLimitAttribute : Attribute
{
}

// The web admin path next to the Mini App one: the cookie counts only on /api/admin* actions that ask for it,
// and a change made with it must come from the admin site itself (CSRF).
public sealed class AdminWebRequestGuard(IOptions<AdminWebOptions> options, IAdminWebSessionStore sessions, IAdminAccessPolicy access)
{
    public bool IsEnabled => options.Value.NormalizedOrigin is not null;

    public void EnsureSameOrigin(HttpRequest request)
    {
        var expected = options.Value.NormalizedOrigin;
        var origin = AdminWebOptions.NormalizeOrigin(request.Headers.Origin.ToString());
        if (expected is null || origin is null || !string.Equals(origin, expected, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("A change on a web admin session must come from the admin site.");
        }
    }

    // The Telegram user id of the signed-in web admin, or null when the request has no web admin cookie.
    public async Task<long?> TryAuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!IsEnabled || AdminWebCookie.Read(context.Request) is not { } token)
        {
            return null;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            EnsureSameOrigin(context.Request);
        }

        var session = await AdminWebSessionAccess.RequireAsync(sessions, access, token, cancellationToken);
        return session.TelegramUserId;
    }

    public static bool HasMiniAppAuthorization(HttpRequest request) => !StringValues.IsNullOrEmpty(request.Headers.Authorization);

    public static AuthenticationException SessionEnded() => new("The web admin session has ended.");
}
