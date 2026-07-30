using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Payments;

public sealed record ValidateContactUnlockCheckoutCommand(string Reference, long TelegramUserId, int AmountUzs) : IRequest<TelegramCheckoutValidationDto>;

public sealed record TelegramCheckoutValidationDto(bool IsApproved, string? ErrorMessage)
{
    public static TelegramCheckoutValidationDto Approved() => new(true, null);
    public static TelegramCheckoutValidationDto Rejected() => new(false, "This invoice is no longer available.");
}

public sealed class ValidateContactUnlockCheckoutValidator : AbstractValidator<ValidateContactUnlockCheckoutCommand>
{
    public ValidateContactUnlockCheckoutValidator()
    {
        RuleFor(command => command.Reference).NotEmpty().MaximumLength(80);
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.AmountUzs).GreaterThan(0);
    }
}

public sealed class ValidateContactUnlockCheckoutHandler(IPaymentOrderRepository paymentOrders, IUnitOfWork unitOfWork) : IRequestHandler<ValidateContactUnlockCheckoutCommand, TelegramCheckoutValidationDto>
{
    public async Task<TelegramCheckoutValidationDto> Handle(ValidateContactUnlockCheckoutCommand command, CancellationToken cancellationToken)
    {
        var paymentOrder = await paymentOrders.GetByReferenceAsync(command.Reference, cancellationToken);
        if (paymentOrder is not null && paymentOrder.ExpireIfOverdue(DateTime.UtcNow))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return TelegramCheckoutValidationDto.Rejected();
        }

        if (paymentOrder is null
            || paymentOrder.PayerTelegramUserId != command.TelegramUserId
            || paymentOrder.AmountUzs != command.AmountUzs
            || paymentOrder.Status != PaymentOrderStatus.Pending)
        {
            return TelegramCheckoutValidationDto.Rejected();
        }

        return TelegramCheckoutValidationDto.Approved();
    }
}
