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
            throw new AuthenticationException("Telegram initData authorization is required.");
        }

        return telegramValidator.Validate(authorization[scheme.Length..]);
    }
}
