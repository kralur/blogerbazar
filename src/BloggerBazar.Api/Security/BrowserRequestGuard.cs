using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.BrowserSessions;
using BloggerBazar.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace BloggerBazar.Api.Security;

// D64 stage B: the browser's cookies. "__Host-": kept only for the Mini App's exact host, over HTTPS, for the whole
// site; HttpOnly keeps them away from page scripts; SameSite=Strict keeps other sites from sending them. The API is
// reached through the Mini App's nginx (/api/), so the cookies stay on that one address.
internal static class BrowserCookies
{
    // The session itself.
    public const string Session = "__Host-bb_web";
    // A sign-in this browser asked for: "<code>.<secret>", for the couple of minutes the code is valid.
    public const string SignIn = "__Host-bb_web_sign_in";

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

    public static string? Read(HttpRequest request, string name) =>
        request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    // Either browser cookie, for the rate limiter: such a request gets the browser budget of its address.
    public static bool HasAny(HttpRequest request) => Read(request, Session) is not null || Read(request, SignIn) is not null;
}

// Business actions a company member may do from a browser session (D64 stage B). Everything without it keeps
// accepting Telegram's signed initData only: admin actions, role switch, account deletion, the blogger and brand face
// profiles, creating a company.
[AttributeUsage(AttributeTargets.Method)]
public sealed class BrowserSessionAllowedAttribute : Attribute
{
}

// An action outside the browser list was called with a browser session cookie: 403 "telegram_only".
public sealed class TelegramOnlyException() : UnauthorizedAccessException("This action is available in Telegram only.")
{
    public const string Code = "telegram_only";
}

// Marks the browser sign-in request: the "api" rate limit policy gives it its own small per-address budget.
[AttributeUsage(AttributeTargets.Method)]
public sealed class BrowserSignInLimitAttribute : Attribute
{
}

public sealed class BrowserRequestGuard(
    IOptions<TelegramOptions> telegramOptions,
    IBrowserSessionStore sessions,
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses)
{
    private const string IdentityItemKey = "browser-session-identity";

    public string? Origin => BrowserSessionOptions.OriginOf(telegramOptions.Value.MiniAppUrl);

    public string BotUsername => telegramOptions.Value.BotUsername.Trim().TrimStart('@');

    // Off (sign-in answers 404) until the Mini App address and the bot are configured.
    public bool IsEnabled => Origin is not null && BotUsername.Length > 0;

    // A change made with a browser cookie must come from the Mini App's own page (CSRF).
    public void EnsureSameOrigin(HttpRequest request)
    {
        var expected = Origin;
        var origin = BrowserSessionOptions.OriginOf(request.Headers.Origin.ToString());
        if (expected is null || origin is null || !string.Equals(origin, expected, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("A change in a browser session must come from the BloggerBazar page.");
        }
    }

    // Reads the session cookie of a request without Telegram's initData; leaves the identity for the controller.
    public async Task AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (!IsEnabled || HasMiniAppAuthorization(context.Request) || BrowserCookies.Read(context.Request, BrowserCookies.Session) is not { } token)
        {
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            EnsureSameOrigin(context.Request);
        }

        context.Items[IdentityItemKey] = await BrowserSessionAccess.RequireAsync(sessions, users, businesses, token, cancellationToken);
    }

    // The browser session behind this request, or null for a Telegram (initData) request.
    public static BrowserSessionIdentity? Current(HttpContext context) =>
        context.Items.TryGetValue(IdentityItemKey, out var value) ? value as BrowserSessionIdentity : null;

    public static bool HasMiniAppAuthorization(HttpRequest request) => !StringValues.IsNullOrEmpty(request.Headers.Authorization);
}

// Runs before model binding on every action, but reads the browser cookie only on actions marked
// [BrowserSessionAllowed]; anywhere else the cookie means nothing.
public sealed class BrowserSessionFilter(BrowserRequestGuard guard) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<BrowserSessionAllowedAttribute>().Any()
            ? guard.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted)
            : Task.CompletedTask;
}
