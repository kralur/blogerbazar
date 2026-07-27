using BloggerBazar.Api.Contracts.CollaborationRequests;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Application.Features.Deals;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/collaboration-requests")]
public sealed class CollaborationRequestsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<CollaborationRequestDto>>> GetMine(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetMyCollaborationRequestsQuery(actor.Id), cancellationToken));
    }

    [HttpPost("bloggers/{bloggerId:guid}")]
    public async Task<ActionResult<CollaborationRequestDto>> Create(Guid bloggerId, CreateCollaborationRequestRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var result = await sender.Send(new CreateCollaborationRequestCommand(bloggerId, actor.Id, request.Message), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("{requestId:guid}/status")]
    public async Task<ActionResult<CollaborationRequestDto>> UpdateStatus(Guid requestId, UpdateCollaborationRequestStatusRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new UpdateCollaborationRequestStatusCommand(requestId, actor.Id, request.Status), cancellationToken));
    }

    [HttpPost("{requestId:guid}/deal")]
    public async Task<ActionResult<DealDto>> CreateDeal(Guid requestId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var deal = await sender.Send(new CreateDealFromCollaborationRequestCommand(requestId, actor.Id), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, deal);
    }
}
