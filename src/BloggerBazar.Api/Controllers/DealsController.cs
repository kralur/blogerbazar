using BloggerBazar.Api.Contracts.Reviews;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Features.Reviews;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/deals")]
public sealed class DealsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("me")]
    [ProducesResponseType<IReadOnlyList<MyDealDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyDealDto>>> GetMine(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetMyDealsQuery(actor.Id), cancellationToken));
    }

    [HttpPost("{dealId:guid}/complete")]
    [ProducesResponseType<DealDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DealDto>> Complete(Guid dealId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new CompleteDealCommand(dealId, actor.Id), cancellationToken));
    }

    [HttpPost("{dealId:guid}/reviews")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReviewDto>> CreateReview(Guid dealId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var review = await sender.Send(new CreateReviewCommand(dealId, actor.Id, request.Rating, request.Comment), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, review);
    }
}
