using BloggerBazar.Api.Contracts.Payments;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpPost("contact-unlocks")]
    [ProducesResponseType<ContactUnlockOrderDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ContactUnlockOrderDto>> CreateContactUnlockOrder(
        CreateContactUnlockOrderRequest request,
        CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var order = await sender.Send(new CreateContactUnlockOrderCommand(actor.Id, request.TargetType, request.TargetId), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, order);
    }

    [HttpPost("contact-unlocks/{reference}/invoice")]
    [ProducesResponseType<TelegramInvoiceLinkResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TelegramInvoiceLinkResponse>> CreateContactUnlockInvoice(
        string reference,
        CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var invoice = await sender.Send(new CreateContactUnlockTelegramInvoiceCommand(reference, actor.Id), cancellationToken);
        return Ok(new TelegramInvoiceLinkResponse(invoice.Reference, invoice.InvoiceLink));
    }

    [HttpGet("contact-unlocks/{targetType}/{targetId:guid}")]
    [ProducesResponseType<ContactUnlockStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ContactUnlockStatusDto>> GetContactUnlockStatus(
        ContactTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        return Ok(await sender.Send(new GetContactUnlockStatusQuery(actor.Id, targetType, targetId), cancellationToken));
    }
}
