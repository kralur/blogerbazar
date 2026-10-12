using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Marketplace;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

// D53: the signed-in "For you" rail; the public, cached home stays in MarketplaceController.
[ApiController]
[Route("api/marketplace/for-me")]
public sealed class RecommendationsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet]
    [ProducesResponseType<RecommendationsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationsDto>> Get(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetRecommendationsQuery(actor.Id), cancellationToken));
    }
}
