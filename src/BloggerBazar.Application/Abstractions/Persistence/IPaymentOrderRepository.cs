using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IPaymentOrderRepository
{
    Task<PaymentOrder?> GetByReferenceAsync(string reference, CancellationToken cancellationToken);
    Task<PaymentOrder?> GetPendingContactUnlockAsync(long payerTelegramUserId, ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
        Task.FromResult<PaymentOrder?>(null);
    Task AddAsync(PaymentOrder paymentOrder, CancellationToken cancellationToken);
}
