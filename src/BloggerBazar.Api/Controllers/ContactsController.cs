using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/contacts")]
public sealed class ContactsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("{targetType}/{targetId:guid}")]
    [ProducesResponseType<ContactDetailsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ContactDetailsDto>> Get(ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken)
    {
        return Ok(await sender.Send(new GetUnlockedContactQuery(targetType, targetId), cancellationToken));
    }
}
