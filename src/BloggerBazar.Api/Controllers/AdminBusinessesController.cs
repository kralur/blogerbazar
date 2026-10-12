using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.Businesses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/admin/businesses")]
public sealed class AdminBusinessesController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminBusinessDto>>> GetAll([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetAdminBusinessesQuery(actor.Id, take), cancellationToken));
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<BusinessProfileDto>>> GetPending(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetPendingBusinessProfilesQuery(actor.Id), cancellationToken));
    }

    [HttpPost("{businessId:guid}/{decision}")]
    public async Task<ActionResult<BusinessProfileDto>> Moderate(Guid businessId, string decision, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return decision switch
        {
            "approve" => Ok(await sender.Send(new ModerateBusinessProfileCommand(businessId, actor.Id, true), cancellationToken)),
            "reject" => Ok(await sender.Send(new ModerateBusinessProfileCommand(businessId, actor.Id, false), cancellationToken)),
            "needs-changes" => Ok(await sender.Send(new ModerateBusinessProfileCommand(businessId, actor.Id, false, true), cancellationToken)),
            _ => BadRequest()
        };
    }
}
