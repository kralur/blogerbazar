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
        var telegramUser = GetTelegramIdentity();
        return Ok(await sender.Send(new GetCurrentPlatformUserCommand(telegramUser.Id, telegramUser.FirstName, telegramUser.Username), cancellationToken));
    }

    [HttpDelete]
    [ProducesResponseType<AccountDeletionResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountDeletionResultDto>> DeleteCurrentAccount(CancellationToken cancellationToken)
    {
        var telegramUser = GetTelegramIdentity();
        return Ok(await sender.Send(new DeleteCurrentAccountCommand(telegramUser.Id, HttpContext.TraceIdentifier), cancellationToken));
    }

    [HttpPut("language")]
    [ProducesResponseType<CurrentPlatformUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CurrentPlatformUserDto>> SetLanguage(SetInterfaceLanguageRequest request, CancellationToken cancellationToken)
    {
        var telegramUser = GetTelegramUser();
        return Ok(await sender.Send(new SetInterfaceLanguageCommand(telegramUser.Id, request.Language), cancellationToken));
    }

    // D50: pause or resume one of the account's role profiles; role is "blogger", "brand-face" or "business".
    [HttpPut("roles/{role}/visibility")]
    [ProducesResponseType<ProfileVisibilityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileVisibilityDto>> SetProfileVisibility(string role, SetProfileVisibilityRequest request, CancellationToken cancellationToken)
    {
        var telegramUser = GetTelegramUser();
        var marketplaceRole = role.ToLowerInvariant() switch
        {
            "blogger" => MarketplaceRole.Blogger,
            "brand-face" => MarketplaceRole.BrandFace,
            "business" => MarketplaceRole.Business,
            _ => throw new InvalidOperationException("Profile was not found.")
        };
        return Ok(await sender.Send(new SetProfileVisibilityCommand(telegramUser.Id, marketplaceRole, request.Hidden), cancellationToken));
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
