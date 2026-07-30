using BloggerBazar.Api.Contracts.Users;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Users;
using BloggerBazar.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/users/me")]
public sealed class UsersController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet]
    public async Task<ActionResult<CurrentPlatformUserDto>> GetCurrent(CancellationToken cancellationToken)
    {
        var telegramUser = GetTelegramUser();
        return Ok(await sender.Send(new GetCurrentPlatformUserCommand(telegramUser.Id, telegramUser.FirstName, telegramUser.Username), cancellationToken));
    }

    [HttpPut("selected-role")]
    public async Task<ActionResult<CurrentPlatformUserDto>> SelectRole(SelectMarketplaceRoleRequest request, CancellationToken cancellationToken)
    {
        var telegramUser = GetTelegramUser();
        if (!Enum.TryParse<MarketplaceRole>(request.Role, true, out var role))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(request.Role)] = ["The marketplace role is invalid."] }));
        }

        return Ok(await sender.Send(new SelectMarketplaceRoleCommand(telegramUser.Id, role), cancellationToken));
    }
}
