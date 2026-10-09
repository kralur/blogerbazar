using BloggerBazar.Api.Contracts.Offers;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Offers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/offers")]
public sealed class OffersController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpPost]
    [ProducesResponseType<OfferDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OfferDto>> Create(CreateOfferRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var offer = await sender.Send(new CreateOfferCommand(actor.Id, request.BloggerId, request.Format, request.OfferedBudget, request.Deadline, request.Message ?? string.Empty, request.BrandFaceId), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, offer);
    }

    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<OfferDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OfferDto>>> GetMine(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetMyOffersQuery(actor.Id), cancellationToken));
    }

    [HttpGet("mine/{offerId:guid}")]
    [ProducesResponseType<OfferDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OfferDto>> GetMineById(Guid offerId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetMyOfferQuery(actor.Id, offerId), cancellationToken));
    }

    [HttpPost("mine/{offerId:guid}/accept")]
    [ProducesResponseType<OfferDecisionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OfferDecisionDto>> Accept(Guid offerId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new AcceptOfferCommand(actor.Id, offerId), cancellationToken));
    }

    [HttpPost("mine/{offerId:guid}/decline")]
    [ProducesResponseType<OfferDecisionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OfferDecisionDto>> Decline(Guid offerId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new DeclineOfferCommand(actor.Id, offerId), cancellationToken));
    }
}
