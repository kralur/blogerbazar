using BloggerBazar.Api.Security;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Users;
using MediatR;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.AspNetCore.Mvc;
using System.Security.Authentication;

namespace BloggerBazar.Api.Controllers;

public abstract class TelegramControllerBase(ITelegramWebAppValidator telegramValidator) : ControllerBase
{
    protected TelegramWebAppUser GetTelegramUser()
    {
        var user = GetTelegramIdentity();
        HttpContext.RequestServices.GetRequiredService<IPlatformUserAccessPolicy>().EnsureActive(user.Id);
        return user;
    }

    // Admin actions only: the Mini App signs every request with initData (unchanged); the web admin sends its session
    // cookie instead. Every other endpoint keeps accepting initData only. The handlers still check IAdminAccessPolicy.
    protected async Task<long> GetAdminActorIdAsync(CancellationToken cancellationToken)
    {
        if (AdminWebRequestGuard.HasMiniAppAuthorization(Request))
        {
            return GetTelegramUser().Id;
        }

        var actorId = await HttpContext.RequestServices.GetRequiredService<AdminWebRequestGuard>().TryAuthenticateAsync(HttpContext, cancellationToken);
        return actorId ?? throw new AuthenticationException("Telegram initData authorization is required.");
    }

    // True when this request comes from a company member's browser session rather than from Telegram.
    protected bool IsBrowserSession => BrowserRequestGuard.Current(HttpContext) is not null;

    // The phone on a profile is the one Telegram confirmed for this user, never the form value (D41).
    protected static async Task<string> RequireVerifiedPhoneAsync(ISender sender, long telegramUserId, CancellationToken cancellationToken) =>
        await sender.Send(new GetVerifiedPhoneQuery(telegramUserId), cancellationToken)
            ?? throw new BusinessRuleConflictException("phone_not_verified", "Share the phone number from Telegram before saving a profile.");

    protected TelegramWebAppUser GetTelegramIdentity()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string scheme = "tma ";
        if (!authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            // D64 stage B: on a [BrowserSessionAllowed] action a company member's browser session stands in for
            // initData; the stored name keeps GetCurrentPlatformUser from changing it.
            if (BrowserRequestGuard.Current(HttpContext) is { } browser)
            {
                return new TelegramWebAppUser(browser.User.TelegramUserId, browser.User.Username, browser.User.FirstName);
            }

            // A browser with a session cookie on an action that is not for browsers: "do it in Telegram", not "sign in".
            if (BrowserCookies.Read(Request, BrowserCookies.Session) is not null)
            {
                throw new TelegramOnlyException();
            }

            throw new AuthenticationException("Telegram initData authorization is required.");
        }

        return telegramValidator.Validate(authorization[scheme.Length..]);
    }
}
