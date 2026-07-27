using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Payments;

public sealed record ConfirmContactUnlockPaymentCommand(string Reference, string ProviderTransactionId, int AmountUzs, long? PayerTelegramUserId = null) : IRequest<ContactUnlockOrderDto>;

public sealed class ConfirmContactUnlockPaymentValidator : AbstractValidator<ConfirmContactUnlockPaymentCommand>
{
    public ConfirmContactUnlockPaymentValidator()
    {
        RuleFor(command => command.Reference).NotEmpty().MaximumLength(80);
        RuleFor(command => command.ProviderTransactionId).NotEmpty().MaximumLength(120);
        RuleFor(command => command.AmountUzs).GreaterThan(0);
        When(command => command.PayerTelegramUserId.HasValue, () =>
            RuleFor(command => command.PayerTelegramUserId!.Value).GreaterThan(0));
    }
}

public sealed class ConfirmContactUnlockPaymentHandler(
    IPaymentOrderRepository paymentOrders,
    IContactUnlockRepository contactUnlocks,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmContactUnlockPaymentCommand, ContactUnlockOrderDto>
{
    public async Task<ContactUnlockOrderDto> Handle(ConfirmContactUnlockPaymentCommand command, CancellationToken cancellationToken)
    {
        var paymentOrder = await paymentOrders.GetByReferenceAsync(command.Reference, cancellationToken)
            ?? throw new InvalidOperationException("Payment order was not found.");
        if (command.PayerTelegramUserId.HasValue && paymentOrder.PayerTelegramUserId != command.PayerTelegramUserId.Value)
        {
            throw new UnauthorizedAccessException("Payment payer does not match the order.");
        }
        if (paymentOrder.AmountUzs != command.AmountUzs)
        {
            throw new InvalidOperationException("Payment amount does not match the order.");
        }

        paymentOrder.MarkPaid(command.ProviderTransactionId);
        var contactUnlock = await contactUnlocks.GetAsync(paymentOrder.PayerTelegramUserId, paymentOrder.TargetType, paymentOrder.TargetId, cancellationToken);
        if (contactUnlock is null)
        {
            contactUnlock = ContactUnlock.Create(paymentOrder);
            await contactUnlocks.AddAsync(contactUnlock, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ContactUnlockOrderDto.From(paymentOrder, true);
    }
}
