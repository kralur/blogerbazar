using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/admin/bloggers")]
public sealed class AdminBloggersController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("pending")]
    [ProducesResponseType<IReadOnlyList<AdminBloggerProfileDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminBloggerProfileDto>>> GetPending([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetPendingBloggerProfilesQuery(actorId, take), cancellationToken));
    }

    [HttpPost("{bloggerId:guid}/approve")]
    [ProducesResponseType<AdminBloggerProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminBloggerProfileDto>> Approve(Guid bloggerId, CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new ModerateBloggerProfileCommand(bloggerId, actorId, true), cancellationToken));
    }

    [HttpPost("{bloggerId:guid}/reject")]
    [ProducesResponseType<AdminBloggerProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminBloggerProfileDto>> Reject(Guid bloggerId, CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new ModerateBloggerProfileCommand(bloggerId, actorId, false), cancellationToken));
    }

    [HttpPost("{bloggerId:guid}/needs-changes")]
    [ProducesResponseType<AdminBloggerProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminBloggerProfileDto>> NeedsChanges(Guid bloggerId, CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new ModerateBloggerProfileCommand(bloggerId, actorId, false, true), cancellationToken));
    }

}
