using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Favorites;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/favorites")]
public sealed class FavoritesController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet]
    [ProducesResponseType<FavoritesPageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FavoritesPageDto>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetFavoritesQuery(actor.Id, page, pageSize), cancellationToken));
    }

    [HttpPost("{bloggerId:guid}")]
    [ProducesResponseType<FavoriteOperationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FavoriteOperationDto>> Save(Guid bloggerId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new SaveFavoriteCommand(actor.Id, bloggerId), cancellationToken));
    }

    [HttpDelete("{bloggerId:guid}")]
    [ProducesResponseType<FavoriteOperationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FavoriteOperationDto>> Delete(Guid bloggerId, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new RemoveFavoriteCommand(actor.Id, bloggerId), cancellationToken));
    }
}
