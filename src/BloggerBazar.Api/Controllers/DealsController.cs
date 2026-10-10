using BloggerBazar.Api.Contracts.Deals;
using BloggerBazar.Api.Contracts.Reviews;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Features.Payments;
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

    [HttpGet("me/{dealId:guid}")]
    [ProducesResponseType<DealDetailsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DealDetailsDto>> GetMineById(Guid dealId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetMyDealQuery(dealId, actor.Id), cancellationToken));
    }

    [HttpGet("me/{dealId:guid}/contact")]
    [ProducesResponseType<ContactDetailsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ContactDetailsDto>> GetMineContact(Guid dealId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetMyDealContactQuery(dealId, actor.Id), cancellationToken));
    }

    [HttpPost("me/{dealId:guid}/contact/share")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ShareMineContact(Guid dealId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        await sender.Send(new ShareDealContactCommand(dealId, actor.Id), cancellationToken);
        return NoContent();
    }

    // D51: the agreed price and links to published ads, both optional.
    [HttpPut("me/{dealId:guid}/price")]
    [ProducesResponseType<DealPriceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DealPriceDto>> SetPrice(Guid dealId, SetDealPriceRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new SetDealPriceCommand(dealId, actor.Id, request.Price), cancellationToken));
    }

    [HttpPost("me/{dealId:guid}/publications")]
    [ProducesResponseType<DealPublicationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DealPublicationDto>> AddPublication(Guid dealId, AddDealPublicationRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var publication = await sender.Send(new AddDealPublicationCommand(dealId, actor.Id, request.Url, request.Views), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, publication);
    }

    [HttpPut("me/{dealId:guid}/publications/{publicationId:guid}")]
    [ProducesResponseType<DealPublicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealPublicationDto>> UpdatePublication(Guid dealId, Guid publicationId, UpdateDealPublicationViewsRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new UpdateDealPublicationViewsCommand(dealId, publicationId, actor.Id, request.Views), cancellationToken));
    }

    [HttpDelete("me/{dealId:guid}/publications/{publicationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePublication(Guid dealId, Guid publicationId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        await sender.Send(new DeleteDealPublicationCommand(dealId, publicationId, actor.Id), cancellationToken);
        return NoContent();
    }

    [HttpPost("me/{dealId:guid}/publications/{publicationId:guid}/confirm")]
    [ProducesResponseType<DealPublicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealPublicationDto>> ConfirmPublication(Guid dealId, Guid publicationId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new ConfirmDealPublicationCommand(dealId, publicationId, actor.Id), cancellationToken));
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
