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
