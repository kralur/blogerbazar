using BloggerBazar.Application.Features.PublicStats;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

// D58: the only API answer that needs no sign-in: three totals for the landing page. The landing site's nginx asks
// for it over Railway's private network and keeps the answer for ten minutes; the API caches it as well.
[ApiController]
[Route("api/public")]
public sealed class PublicStatsController(ISender sender) : ControllerBase
{
    [HttpGet("stats")]
    [ProducesResponseType<PublicStatsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PublicStatsDto>> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "public, max-age=600";
        return Ok(await sender.Send(new GetPublicStatsQuery(), cancellationToken));
    }
}
