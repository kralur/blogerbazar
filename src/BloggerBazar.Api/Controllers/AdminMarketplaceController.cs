using BloggerBazar.Api.Contracts.Admin;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminMarketplaceController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("bloggers")]
    public async Task<ActionResult<IReadOnlyList<AdminBloggerProfileDto>>> GetBloggers([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetAdminBloggersQuery(actor.Id, take), cancellationToken));
    }

    [HttpGet("collaboration-requests")]
    public async Task<ActionResult<IReadOnlyList<CollaborationRequestDto>>> GetCollaborationRequests([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetAdminCollaborationRequestsQuery(actor.Id, take), cancellationToken));
    }

    [HttpGet("campaigns")]
    public async Task<ActionResult<IReadOnlyList<AdminCampaignDto>>> GetCampaigns([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetAdminCampaignsQuery(actor.Id, take), cancellationToken));
    }

    [HttpPatch("campaigns/{campaignId:guid}")]
    public async Task<ActionResult<AdminCampaignDto>> ModerateCampaign(Guid campaignId, ModerateCampaignRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new ModerateCampaignCommand(campaignId, actor.Id, request.Status, request.IsPromoted), cancellationToken));
    }
}
