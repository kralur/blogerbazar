using BloggerBazar.Application.Abstractions.Security;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

public abstract class TelegramControllerBase(ITelegramWebAppValidator telegramValidator) : ControllerBase
{
    protected TelegramWebAppUser GetTelegramUser()
    {
        var authorization = Request.Headers.Authorization.ToString();
        const string scheme = "tma ";
        if (!authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Telegram initData authorization is required.");
        }

        return telegramValidator.Validate(authorization[scheme.Length..]);
    }
}
