using BloggerBazar.Application.Features.Marketplace;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/marketplace")]
public sealed class MarketplaceController(ISender sender) : ControllerBase
{
    [HttpGet("home")]
    [ProducesResponseType<MarketplaceHomeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketplaceHomeDto>> GetHome(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMarketplaceHomeQuery(), cancellationToken));

    [HttpGet("taxonomy/categories")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(CancellationToken cancellationToken) =>
        Ok((await sender.Send(new GetMarketplaceHomeQuery(), cancellationToken)).Categories);
}
