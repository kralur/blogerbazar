using BloggerBazar.Application.Abstractions.Payments;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Payments;

public sealed record CreateContactUnlockOrderCommand(long TelegramUserId, ContactTargetType TargetType, Guid TargetId) : IRequest<ContactUnlockOrderDto>;

public sealed class CreateContactUnlockOrderValidator : AbstractValidator<CreateContactUnlockOrderCommand>
{
    public CreateContactUnlockOrderValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.TargetId).NotEmpty();
        RuleFor(command => command.TargetType).IsInEnum();
    }
}

public sealed class CreateContactUnlockOrderHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IContactUnlockRepository contactUnlocks,
    IPaymentOrderRepository paymentOrders,
    IContactUnlockPricing pricing,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateContactUnlockOrderCommand, ContactUnlockOrderDto>
{
    public async Task<ContactUnlockOrderDto> Handle(CreateContactUnlockOrderCommand command, CancellationToken cancellationToken)
    {
        var targetTelegramUserId = command.TargetType switch
        {
            ContactTargetType.Blogger => (await bloggers.GetByIdAsync(command.TargetId, cancellationToken))?.TelegramUserId,
            ContactTargetType.Business => (await businesses.GetByIdAsync(command.TargetId, cancellationToken))?.TelegramUserId,
            _ => null
        };

        if (targetTelegramUserId is null)
        {
            throw new InvalidOperationException("Contact target was not found.");
        }

        if (targetTelegramUserId == command.TelegramUserId)
        {
            throw new InvalidOperationException("You cannot unlock your own contact.");
        }

        var existingUnlock = await contactUnlocks.GetAsync(command.TelegramUserId, command.TargetType, command.TargetId, cancellationToken);
        if (existingUnlock is not null)
        {
            return ContactUnlockOrderDto.From(existingUnlock.PaymentOrder, true);
        }

        var pendingOrder = await paymentOrders.GetPendingContactUnlockAsync(command.TelegramUserId, command.TargetType, command.TargetId, cancellationToken);
        if (pendingOrder is not null)
        {
            if (!pendingOrder.ExpireIfOverdue(DateTime.UtcNow))
            {
                return ContactUnlockOrderDto.From(pendingOrder, false);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var paymentOrder = PaymentOrder.CreateContactUnlock(command.TelegramUserId, command.TargetType, command.TargetId, pricing.AmountUzs, DateTime.UtcNow.Add(pricing.PendingOrderLifetime));
        await paymentOrders.AddAsync(paymentOrder, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ContactUnlockOrderDto.From(paymentOrder, false);
    }
}
