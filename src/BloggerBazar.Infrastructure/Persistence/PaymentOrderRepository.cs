using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class PaymentOrderRepository(BloggerBazarDbContext dbContext) : IPaymentOrderRepository
{
    public Task<PaymentOrder?> GetByReferenceAsync(string reference, CancellationToken cancellationToken) =>
        dbContext.PaymentOrders.SingleOrDefaultAsync(order => order.Reference == reference, cancellationToken);

    public Task<PaymentOrder?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken cancellationToken) =>
        dbContext.PaymentOrders.SingleOrDefaultAsync(order => order.ProviderTransactionId == providerTransactionId, cancellationToken);

    public Task<PaymentOrder?> GetPendingContactUnlockAsync(long payerTelegramUserId, ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
        dbContext.PaymentOrders.OrderByDescending(order => order.CreatedAtUtc).FirstOrDefaultAsync(
            order => order.PayerTelegramUserId == payerTelegramUserId
                && order.TargetType == targetType
                && order.TargetId == targetId
                && order.Status == PaymentOrderStatus.Pending,
            cancellationToken);

    public async Task AddAsync(PaymentOrder paymentOrder, CancellationToken cancellationToken) =>
        await dbContext.PaymentOrders.AddAsync(paymentOrder, cancellationToken);
}
