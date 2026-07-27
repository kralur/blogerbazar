using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Api.Contracts.Campaigns;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/campaign-applications")]
public sealed class CampaignApplicationsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyList<MyCampaignApplicationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyCampaignApplicationDto>>> GetMine(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetMyCampaignApplicationsQuery(actor.Id), cancellationToken));
    }

    [HttpPost("{applicationId:guid}/accept")]
    [ProducesResponseType<DealDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DealDto>> Accept(Guid applicationId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var deal = await sender.Send(new AcceptCampaignApplicationCommand(applicationId, actor.Id), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, deal);
    }

    [HttpPatch("{applicationId:guid}/status")]
    public async Task<ActionResult<MyCampaignApplicationDto>> UpdateStatus(Guid applicationId, UpdateCampaignApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new UpdateCampaignApplicationStatusCommand(applicationId, actor.Id, request.Status), cancellationToken));
    }
}
