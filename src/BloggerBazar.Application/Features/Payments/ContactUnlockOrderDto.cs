using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Features.Payments;

public sealed record ContactUnlockOrderDto(
    string Reference,
    ContactTargetType TargetType,
    Guid TargetId,
    int AmountUzs,
    PaymentOrderStatus Status,
    bool IsUnlocked,
    DateTime ExpiresAtUtc)
{
    public static ContactUnlockOrderDto From(PaymentOrder order, bool isUnlocked) =>
        new(order.Reference, order.TargetType, order.TargetId, order.AmountUzs, order.Status, isUnlocked, order.ExpiresAtUtc);
}
