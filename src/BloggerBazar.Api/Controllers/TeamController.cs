using BloggerBazar.Api.Security;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Team;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

// D64 stage C: the company's team. Members work with it from Telegram and from a signed-in browser alike; asking to
// join (the invitation link opens the Mini App) and "work on a computer" exist in Telegram only.
[ApiController]
[Route("api/team")]
public sealed class TeamController(ISender sender, BrowserRequestGuard guard, ITelegramWebAppValidator telegramValidator)
    : TelegramControllerBase(telegramValidator)
{
    // The link that opens the Mini App with an invitation: t.me/<bot>?startapp=join_<code>.
    private const string StartParameterPrefix = "join_";

    [HttpGet]
    [BrowserSessionAllowed]
    [ProducesResponseType<TeamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> Get(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var team = await sender.Send(new GetTeamQuery(actor.Id), cancellationToken);
        return team is null ? NotFound() : Ok(team);
    }

    [HttpPut("mode")]
    [BrowserSessionAllowed]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<TeamDto>> SetMode(SetTeamModeRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new SetTeamModeCommand(actor.Id, request.TeamMode), cancellationToken));
    }

    [HttpPost("invitations")]
    [BrowserSessionAllowed]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<TeamInvitationLinkResponse>> CreateInvitation(CreateTeamInvitationRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var created = await sender.Send(new CreateTeamInvitationCommand(actor.Id, request.Role), cancellationToken);
        var link = guard.BotUsername.Length > 0 ? $"https://t.me/{guard.BotUsername}?startapp={StartParameterPrefix}{created.Code}" : null;
        return Ok(new TeamInvitationLinkResponse(created.Id, created.Role, created.ExpiresAtUtc, link));
    }

    [HttpDelete("invitations/{id:guid}")]
    [BrowserSessionAllowed]
    public async Task<IActionResult> RevokeInvitation(Guid id, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        await sender.Send(new RevokeTeamInvitationCommand(actor.Id, id), cancellationToken);
        return NoContent();
    }

    // The person who opened the link, in Telegram: what company and role it is for.
    [HttpPost("invitations/preview")]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<TeamInvitationPreviewDto>> PreviewInvitation(TeamInvitationCodeRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new PreviewTeamInvitationQuery(actor.Id, request.Code), cancellationToken));
    }

    [HttpPost("invitations/request")]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<TeamInvitationPreviewDto>> RequestAccess(TeamInvitationCodeRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new RequestTeamAccessCommand(actor.Id, request.Code), cancellationToken));
    }

    [HttpPost("requests/{id:guid}/accept")]
    [BrowserSessionAllowed]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<TeamDto>> AcceptRequest(Guid id, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new AcceptTeamRequestCommand(actor.Id, id, guard.Origin), cancellationToken));
    }

    [HttpPost("requests/{id:guid}/decline")]
    [BrowserSessionAllowed]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<TeamDto>> DeclineRequest(Guid id, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new DeclineTeamRequestCommand(actor.Id, id), cancellationToken));
    }

    [HttpPut("members/{id:guid}/role")]
    [BrowserSessionAllowed]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<TeamDto>> ChangeRole(Guid id, ChangeTeamRoleRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new ChangeTeamRoleCommand(actor.Id, id, request.Role), cancellationToken));
    }

    [HttpDelete("members/{id:guid}")]
    [BrowserSessionAllowed]
    public async Task<ActionResult<TeamDto>> RemoveMember(Guid id, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new RemoveTeamMemberCommand(actor.Id, id), cancellationToken));
    }

    [HttpPost("leave")]
    [BrowserSessionAllowed]
    [RequestSizeLimit(1_024)]
    public async Task<IActionResult> Leave(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        await sender.Send(new LeaveTeamCommand(actor.Id), cancellationToken);
        return NoContent();
    }

    // «Работать на компьютере» in the Mini App: the bot puts the site's address into the chat; the app opens it too.
    [HttpPost("web-link")]
    [RequestSizeLimit(1_024)]
    public async Task<ActionResult<WorkOnComputerResponse>> SendWebLink(CancellationToken cancellationToken)
    {
        if (!guard.IsEnabled || guard.Origin is not { } site) return NotFound();
        var actor = GetTelegramUser();
        await sender.Send(new SendWorkOnComputerLinkCommand(actor.Id, site), cancellationToken);
        return Ok(new WorkOnComputerResponse(site));
    }
}

public sealed record SetTeamModeRequest(bool TeamMode);
public sealed record CreateTeamInvitationRequest(string Role);
public sealed record ChangeTeamRoleRequest(string Role);
public sealed record TeamInvitationCodeRequest(string Code);
// Link: null while the bot's name is not configured.
public sealed record TeamInvitationLinkResponse(Guid Id, string Role, DateTime ExpiresAtUtc, string? Link);
public sealed record WorkOnComputerResponse(string Url);
