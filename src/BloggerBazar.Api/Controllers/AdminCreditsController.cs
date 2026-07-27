using BloggerBazar.Api.Contracts.Admin;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Wallet;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/admin/credits")]
public sealed class AdminCreditsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpPost("grant")]
    public async Task<ActionResult<WalletDto>> Grant(GrantCreditsRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GrantCreditsCommand(actor.Id, request.TelegramUserId, request.Amount, request.Source, request.Note), cancellationToken));
    }
}
