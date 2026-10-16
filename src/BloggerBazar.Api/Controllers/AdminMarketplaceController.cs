using BloggerBazar.Api.Contracts.Admin;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Application.Features.Tips;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminMarketplaceController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminPlatformUserDto>>> GetUsers([FromQuery] int take = 100, [FromQuery] string? search = null, [FromQuery] string? filter = null, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminUsersQuery(actorId, take, search, filter), cancellationToken));
    }

    [HttpGet("users/{telegramUserId:long}")]
    public async Task<ActionResult<AdminUserDetailsDto>> GetUser(long telegramUserId, CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminUserDetailsQuery(actorId, telegramUserId), cancellationToken));
    }

    [HttpPatch("users/{telegramUserId:long}/role")]
    public async Task<ActionResult<AdminPlatformUserDto>> UpdateRole(long telegramUserId, UpdatePlatformUserRoleRequest request, CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        if (!Enum.IsDefined(typeof(BloggerBazar.Domain.Enums.PlatformRole), request.Role)) return BadRequest();
        return Ok(await sender.Send(new UpdatePlatformUserRoleCommand(actorId, telegramUserId, (BloggerBazar.Domain.Enums.PlatformRole)request.Role), cancellationToken));
    }

    [HttpPatch("users/{telegramUserId:long}/blocked")]
    public async Task<ActionResult<AdminPlatformUserDto>> SetBlocked(long telegramUserId, SetPlatformUserBlockedRequest request, CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new SetPlatformUserBlockedCommand(actorId, telegramUserId, request.IsBlocked), cancellationToken));
    }

    // D54: the next bot tip to the admin's own chat, to check how tips look.
    [HttpPost("tips/preview")]
    public async Task<ActionResult<BotTipPreviewDto>> PreviewTip(CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new SendBotTipPreviewCommand(actorId), cancellationToken));
    }

    [HttpGet("audit-logs")]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> GetAuditLogs([FromQuery] int take = 100, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAuditLogQuery(actorId, take, from, to), cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminDashboardQuery(actorId), cancellationToken));
    }

    // Funnel from the first app open to a review, per role; period "7d", "30d" (default) or "all".
    [HttpGet("funnel")]
    public async Task<ActionResult<AdminFunnelDto>> GetFunnel([FromQuery] string? period = null, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminFunnelQuery(actorId, period), cancellationToken));
    }

    [HttpGet("bloggers")]
    public async Task<ActionResult<IReadOnlyList<AdminBloggerProfileDto>>> GetBloggers([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminBloggersQuery(actorId, take), cancellationToken));
    }

    // Web admin deal list; status "active", "completed" or "cancelled", every deal without it.
    [HttpGet("deals")]
    public async Task<ActionResult<IReadOnlyList<AdminDealDto>>> GetDeals([FromQuery] string? status = null, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminDealsQuery(actorId, status, take), cancellationToken));
    }

    [HttpGet("collaboration-requests")]
    public async Task<ActionResult<IReadOnlyList<CollaborationRequestDto>>> GetCollaborationRequests([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminCollaborationRequestsQuery(actorId, take), cancellationToken));
    }

    [HttpGet("campaigns")]
    public async Task<ActionResult<IReadOnlyList<AdminCampaignDto>>> GetCampaigns([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminCampaignsQuery(actorId, take), cancellationToken));
    }

    [HttpPatch("campaigns/{campaignId:guid}")]
    public async Task<ActionResult<AdminCampaignDto>> ModerateCampaign(Guid campaignId, ModerateCampaignRequest request, CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new ModerateCampaignCommand(campaignId, actorId, request.Status, request.IsPromoted), cancellationToken));
    }
}
