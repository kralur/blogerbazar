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
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminPlatformUserDto>>> GetUsers([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetAdminUsersQuery(actor.Id, take), cancellationToken));
    }

    [HttpPatch("users/{telegramUserId:long}/role")]
    public async Task<ActionResult<AdminPlatformUserDto>> UpdateRole(long telegramUserId, UpdatePlatformUserRoleRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        if (!Enum.IsDefined(typeof(BloggerBazar.Domain.Enums.PlatformRole), request.Role)) return BadRequest();
        return Ok(await sender.Send(new UpdatePlatformUserRoleCommand(actor.Id, telegramUserId, (BloggerBazar.Domain.Enums.PlatformRole)request.Role), cancellationToken));
    }

    [HttpPatch("users/{telegramUserId:long}/blocked")]
    public async Task<ActionResult<AdminPlatformUserDto>> SetBlocked(long telegramUserId, SetPlatformUserBlockedRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new SetPlatformUserBlockedCommand(actor.Id, telegramUserId, request.IsBlocked), cancellationToken));
    }

    [HttpGet("audit-logs")]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> GetAuditLogs([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetAuditLogQuery(actor.Id, take), cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetAdminDashboardQuery(actor.Id), cancellationToken));
    }

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
