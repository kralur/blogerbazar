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
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetPendingBloggerProfilesQuery(actor.Id, take), cancellationToken));
    }

    [HttpPost("{bloggerId:guid}/approve")]
    [ProducesResponseType<AdminBloggerProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminBloggerProfileDto>> Approve(Guid bloggerId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new ModerateBloggerProfileCommand(bloggerId, actor.Id, true), cancellationToken));
    }

    [HttpPost("{bloggerId:guid}/reject")]
    [ProducesResponseType<AdminBloggerProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminBloggerProfileDto>> Reject(Guid bloggerId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new ModerateBloggerProfileCommand(bloggerId, actor.Id, false), cancellationToken));
    }

}
