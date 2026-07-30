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

        var user = telegramValidator.Validate(authorization[scheme.Length..]);
        HttpContext.RequestServices.GetRequiredService<IPlatformUserAccessPolicy>().EnsureActive(user.Id);
        return user;
    }
}
