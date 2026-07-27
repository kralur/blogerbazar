using BloggerBazar.Api.Contracts.Campaigns;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Campaigns;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
public sealed class CampaignsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet]
    [ProducesResponseType<SearchCampaignsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchCampaignsResponse>> Search(
        [FromQuery] string? city,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(new SearchCampaignsResponse(await sender.Send(new SearchCampaignsQuery(city, category, page, pageSize), cancellationToken)));

    [HttpGet("{campaignId:guid}")]
    [ProducesResponseType<CampaignDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignDto>> GetById(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await sender.Send(new GetCampaignQuery(campaignId), cancellationToken);
        return campaign is null ? NotFound() : Ok(campaign);
    }

    [HttpPost]
    [ProducesResponseType<CampaignDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CampaignDto>> Create(CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var campaign = await sender.Send(new CreateCampaignCommand(actor.Id, request.Title, request.Description, request.City, request.Categories, request.BudgetFrom, request.BudgetTo, request.PublishImmediately), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, campaign);
    }

    [HttpPost("{campaignId:guid}/applications")]
    [ProducesResponseType<CampaignApplicationDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CampaignApplicationDto>> Apply(Guid campaignId, ApplyToCampaignRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var application = await sender.Send(new ApplyToCampaignCommand(campaignId, actor.Id, request.Message), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, application);
    }
}
