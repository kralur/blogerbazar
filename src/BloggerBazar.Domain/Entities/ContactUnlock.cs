using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class ContactUnlock
{
    private ContactUnlock() { }

    private ContactUnlock(PaymentOrder paymentOrder)
    {
        Id = Guid.NewGuid();
        PaymentOrderId = paymentOrder.Id;
        ViewerTelegramUserId = paymentOrder.PayerTelegramUserId;
        TargetType = paymentOrder.TargetType;
        TargetId = paymentOrder.TargetId;
        UnlockedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PaymentOrderId { get; private set; }
    public PaymentOrder PaymentOrder { get; private set; } = null!;
    public long ViewerTelegramUserId { get; private set; }
    public ContactTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public DateTime UnlockedAtUtc { get; private set; }

    public static ContactUnlock Create(PaymentOrder paymentOrder)
    {
        if (paymentOrder.Status != PaymentOrderStatus.Paid)
        {
            throw new InvalidOperationException("A contact can only be unlocked after a successful payment.");
        }

        return new ContactUnlock(paymentOrder);
    }
}
