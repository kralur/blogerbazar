using BloggerBazar.Api.Contracts.BrandFaces;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.BrandFaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/brand-faces")]
public sealed class BrandFacesController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BrandFacePublicDto>>> Search([FromQuery] string? query, [FromQuery] string? city, [FromQuery] string? category, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new SearchBrandFacesQuery(query, city, category, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BrandFacePublicDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var profile = await sender.Send(new GetBrandFaceQuery(id), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("me")]
    public async Task<ActionResult<BrandFaceProfileDto>> GetMine(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var profile = await sender.Send(new GetMyBrandFaceProfileQuery(actor.Id), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    public async Task<ActionResult<BrandFaceProfileDto>> Upsert(UpsertBrandFaceProfileRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new UpsertBrandFaceProfileCommand(actor.Id, request.Name, request.City, request.Age, request.Gender, request.Languages, request.Categories, request.Experience, request.Instagram, request.Telegram, request.PortfolioUrl, request.CollaborationPrice, request.Description, request.AvatarUrl), cancellationToken));
    }
}
