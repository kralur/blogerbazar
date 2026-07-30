using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

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
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<ConfirmContactUnlockPaymentHandler>? logger = null) : IRequestHandler<ConfirmContactUnlockPaymentCommand, ContactUnlockOrderDto>
{
    public async Task<ContactUnlockOrderDto> Handle(ConfirmContactUnlockPaymentCommand command, CancellationToken cancellationToken)
    {
        var outcome = await unitOfWork.ExecuteInTransactionAsync(
            transactionCancellationToken => ConfirmAsync(command, transactionCancellationToken),
            cancellationToken);

        if (outcome.IsExpired)
        {
            throw new InvalidOperationException("Payment order has expired. Create a new order.");
        }

        if (outcome.SendNotification)
        {
            await BestEffortTelegramNotification.SendAsync(botClient, logger, outcome.TelegramUserId, "BloggerBazar: оплата подтверждена, контакты разблокированы.", cancellationToken);
        }

        return outcome.Order!;
    }

    private async Task<ConfirmationOutcome> ConfirmAsync(ConfirmContactUnlockPaymentCommand command, CancellationToken cancellationToken)
    {
        var paymentOrder = await paymentOrders.GetByProviderTransactionIdAsync(command.ProviderTransactionId, cancellationToken);
        if (paymentOrder is not null)
        {
            EnsurePaymentMatches(paymentOrder, command);
            var existingUnlock = await contactUnlocks.GetAsync(paymentOrder.PayerTelegramUserId, paymentOrder.TargetType, paymentOrder.TargetId, cancellationToken);
            if (existingUnlock is not null)
            {
                logger?.LogInformation("Duplicate Telegram payment delivery was ignored.");
                return ConfirmationOutcome.Completed(ContactUnlockOrderDto.From(paymentOrder, true));
            }
        }

        paymentOrder ??= await paymentOrders.GetByReferenceAsync(command.Reference, cancellationToken)
            ?? throw new InvalidOperationException("Payment order was not found.");
        EnsurePaymentMatches(paymentOrder, command);

        if (paymentOrder.ExpireIfOverdue(DateTime.UtcNow))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ConfirmationOutcome.Expired();
        }

        paymentOrder.MarkPaid(command.ProviderTransactionId);
        var contactUnlock = await contactUnlocks.GetAsync(paymentOrder.PayerTelegramUserId, paymentOrder.TargetType, paymentOrder.TargetId, cancellationToken);
        if (contactUnlock is null)
        {
            contactUnlock = ContactUnlock.Create(paymentOrder);
            await contactUnlocks.AddAsync(contactUnlock, cancellationToken);
        }

        if (await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            return ConfirmationOutcome.Completed(ContactUnlockOrderDto.From(paymentOrder, true), paymentOrder.PayerTelegramUserId);
        }

        var completedPayment = await paymentOrders.GetByProviderTransactionIdAsync(command.ProviderTransactionId, cancellationToken);
        if (completedPayment is not null)
        {
            EnsurePaymentMatches(completedPayment, command);
            var completedUnlock = await contactUnlocks.GetAsync(completedPayment.PayerTelegramUserId, completedPayment.TargetType, completedPayment.TargetId, cancellationToken);
            if (completedUnlock is not null)
            {
                logger?.LogInformation("Concurrent Telegram payment delivery was ignored.");
                return ConfirmationOutcome.Completed(ContactUnlockOrderDto.From(completedPayment, true));
            }
        }

        throw new InvalidOperationException("Payment confirmation could not be completed.");
    }

    private static void EnsurePaymentMatches(PaymentOrder paymentOrder, ConfirmContactUnlockPaymentCommand command)
    {
        if (paymentOrder.Reference != command.Reference
            || (command.PayerTelegramUserId.HasValue && paymentOrder.PayerTelegramUserId != command.PayerTelegramUserId.Value))
        {
            throw new UnauthorizedAccessException("Payment details do not match the order.");
        }

        if (paymentOrder.AmountUzs != command.AmountUzs)
        {
            throw new InvalidOperationException("Payment amount does not match the order.");
        }
    }

    private sealed record ConfirmationOutcome(ContactUnlockOrderDto? Order, long TelegramUserId, bool SendNotification, bool IsExpired)
    {
        public static ConfirmationOutcome Completed(ContactUnlockOrderDto order, long? telegramUserId = null) =>
            new(order, telegramUserId ?? 0, telegramUserId.HasValue, false);

        public static ConfirmationOutcome Expired() => new(null, 0, false, true);
    }
}
