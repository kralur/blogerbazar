using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Marketplace;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

// Catalog reads need the Telegram signature too (D59): the Mini App sends it with every request, and without it the
// whole catalog could be read by anyone without a Telegram account.
[ApiController]
[Route("api/marketplace")]
public sealed class MarketplaceController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("home")]
    [ProducesResponseType<MarketplaceHomeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketplaceHomeDto>> GetHome(CancellationToken cancellationToken)
    {
        GetTelegramUser();
        return Ok(await sender.Send(new GetMarketplaceHomeQuery(), cancellationToken));
    }

    [HttpGet("taxonomy/categories")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken cancellationToken)
    {
        GetTelegramUser();
        return Ok((await sender.Send(new GetMarketplaceHomeQuery(), cancellationToken)).Categories);
    }
}
