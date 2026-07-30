using BloggerBazar.Api.Contracts.Wallet;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Wallet;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/wallet")]
public sealed class WalletController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet]
    public async Task<ActionResult<WalletDto>> Get(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetWalletQuery(actor.Id), cancellationToken));
    }

    [HttpPost("contact-unlocks")]
    [Obsolete("Legacy compatibility endpoint. Contacts are publicly available in BloggerBazar v1.")]
    public async Task<ActionResult> UnlockContact(UnlockContactWithCreditsRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var charged = await sender.Send(new UnlockContactWithCreditsCommand(actor.Id, request.TargetType, request.TargetId), cancellationToken);
        return Ok(new { charged });
    }
}
