using BloggerBazar.Api.Contracts.Businesses;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Businesses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/businesses")]
public sealed class BusinessesController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("me")]
    [ProducesResponseType<MyBusinessProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyBusinessProfileDto>> GetMine(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var profile = await sender.Send(new GetMyBusinessProfileQuery(actor.Id), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost]
    [ProducesResponseType<BusinessProfileDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BusinessProfileDto>> Create(CreateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var profile = await sender.Send(new CreateBusinessProfileCommand(actor.Id, request.Name, request.Username, request.City, request.LogoUrl, request.WebsiteUrl, request.Description, request.Phone, request.Email), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, profile);
    }

    [HttpPut("me")]
    [ProducesResponseType<BusinessProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BusinessProfileDto>> Update(CreateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new UpdateBusinessProfileCommand(actor.Id, request.Name, request.Username, request.City, request.LogoUrl, request.WebsiteUrl, request.Description, request.Phone, request.Email), cancellationToken));
    }
}
