using BloggerBazar.Api.Contracts.Complaints;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Complaints;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

// D52: reports from any role and the admin queue that handles them.
[ApiController]
public sealed class ComplaintsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpPost("api/complaints")]
    [ProducesResponseType<ComplaintDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ComplaintDto>> Create(CreateComplaintRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new CreateComplaintCommand(actor.Id, request.TargetType, request.TargetId, request.Reason, request.Comment), cancellationToken));
    }

    [HttpGet("api/admin/complaints")]
    public async Task<ActionResult<IReadOnlyList<AdminComplaintDto>>> GetForAdmin([FromQuery] string? status = ComplaintStatuses.Open, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new GetAdminComplaintsQuery(actorId, status, take), cancellationToken));
    }

    [HttpPatch("api/admin/complaints/{complaintId:guid}")]
    public async Task<ActionResult<ComplaintDto>> Close(Guid complaintId, CloseComplaintRequest request, CancellationToken cancellationToken)
    {
        var actorId = await GetAdminActorIdAsync(cancellationToken);
        return Ok(await sender.Send(new CloseComplaintCommand(actorId, complaintId, request.Status, request.Note), cancellationToken));
    }
}
