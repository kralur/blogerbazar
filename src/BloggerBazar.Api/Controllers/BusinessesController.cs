using BloggerBazar.Application.Validation;
using BloggerBazar.Api.Contracts.Businesses;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Application.Features.Reviews;
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PublicBusinessProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicBusinessProfileDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var profile = await sender.Send(new GetPublicBusinessProfileQuery(id), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("{id:guid}/reviews")]
    [ProducesResponseType<BusinessReviewsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BusinessReviewsDto>> GetReviews(Guid id, [FromQuery] int take = 20, [FromQuery] int skip = 0, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetBusinessReviewsQuery(id, take, skip), cancellationToken));

    [HttpPost]
    [ProducesResponseType<BusinessProfileDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BusinessProfileDto>> Create(CreateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var phone = await RequireVerifiedPhoneAsync(sender, actor.Id, cancellationToken);
        var profile = await sender.Send(new CreateBusinessProfileCommand(actor.Id, request.Name, ContactValidation.TelegramHandle(actor.Username), request.City, request.LogoUrl, request.WebsiteUrl, request.Description, phone, request.Email), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, profile);
    }

    [HttpPut("me")]
    [ProducesResponseType<BusinessProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BusinessProfileDto>> Update(CreateBusinessProfileRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var phone = await RequireVerifiedPhoneAsync(sender, actor.Id, cancellationToken);
        return Ok(await sender.Send(new UpdateBusinessProfileCommand(actor.Id, request.Name, ContactValidation.TelegramHandle(actor.Username), request.City, request.LogoUrl, request.WebsiteUrl, request.Description, phone, request.Email), cancellationToken));
    }
}
