using BloggerBazar.Application.Abstractions.Payments;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Payments;

public sealed record CreateContactUnlockTelegramInvoiceCommand(string Reference, long TelegramUserId) : IRequest<TelegramInvoiceLinkDto>;

public sealed record TelegramInvoiceLinkDto(string Reference, string InvoiceLink);

public sealed class CreateContactUnlockTelegramInvoiceValidator : AbstractValidator<CreateContactUnlockTelegramInvoiceCommand>
{
    public CreateContactUnlockTelegramInvoiceValidator()
    {
        RuleFor(command => command.Reference).NotEmpty().MaximumLength(80);
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class CreateContactUnlockTelegramInvoiceHandler(
    IPaymentOrderRepository paymentOrders,
    ITelegramPaymentGateway paymentGateway) : IRequestHandler<CreateContactUnlockTelegramInvoiceCommand, TelegramInvoiceLinkDto>
{
    public async Task<TelegramInvoiceLinkDto> Handle(CreateContactUnlockTelegramInvoiceCommand command, CancellationToken cancellationToken)
    {
        var paymentOrder = await paymentOrders.GetByReferenceAsync(command.Reference, cancellationToken)
            ?? throw new InvalidOperationException("Payment order was not found.");

        if (paymentOrder.PayerTelegramUserId != command.TelegramUserId)
        {
            throw new UnauthorizedAccessException("You cannot pay for another user's order.");
        }

        if (paymentOrder.Status != PaymentOrderStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payment orders can be invoiced.");
        }

        var invoiceLink = await paymentGateway.CreateInvoiceLinkAsync(
            new TelegramInvoiceRequest(
                "BloggerBazar",
                "Unlock direct contact",
                paymentOrder.Reference,
                paymentOrder.AmountUzs),
            cancellationToken);

        return new TelegramInvoiceLinkDto(paymentOrder.Reference, invoiceLink);
    }
}
