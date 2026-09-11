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
    [HttpGet("mine")]
    [ProducesResponseType<MyCampaignApplicationsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MyCampaignApplicationsResult>> GetMyPage(
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetMyCampaignApplicationsPageQuery(actor.Id, status, page, pageSize), cancellationToken));
    }

    [HttpGet("mine/{applicationId:guid}")]
    [ProducesResponseType<MyCampaignApplicationDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyCampaignApplicationDetailsDto>> GetMyById(Guid applicationId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var application = await sender.Send(new GetMyCampaignApplicationDetailsQuery(actor.Id, applicationId), cancellationToken);
        return application is null ? NotFound() : Ok(application);
    }

    [HttpPost("mine/{applicationId:guid}/withdraw")]
    [ProducesResponseType<CampaignApplicationDecisionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CampaignApplicationDecisionDto>> Withdraw(Guid applicationId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new WithdrawMyCampaignApplicationCommand(actor.Id, applicationId), cancellationToken));
    }

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
