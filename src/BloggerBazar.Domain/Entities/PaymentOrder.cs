using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class PaymentOrder
{
    private PaymentOrder() { }

    private PaymentOrder(long payerTelegramUserId, ContactTargetType targetType, Guid targetId, int amountUzs, PaymentProvider provider, DateTime? expiresAtUtc = null)
    {
        Id = Guid.NewGuid();
        Reference = $"unlock-{Id:N}";
        PayerTelegramUserId = payerTelegramUserId;
        TargetType = targetType;
        TargetId = targetId;
        AmountUzs = amountUzs;
        Provider = provider;
        Status = PaymentOrderStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc ?? CreatedAtUtc.AddMinutes(30);
    }

    public Guid Id { get; private set; }
    public string Reference { get; private set; } = null!;
    public long PayerTelegramUserId { get; private set; }
    public ContactTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public int AmountUzs { get; private set; }
    public PaymentProvider Provider { get; private set; }
    public PaymentOrderStatus Status { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public ContactUnlock? ContactUnlock { get; private set; }

    public static PaymentOrder CreateContactUnlock(long payerTelegramUserId, ContactTargetType targetType, Guid targetId, int amountUzs, DateTime? expiresAtUtc = null)
    {
        if (amountUzs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountUzs));
        }

        return new PaymentOrder(payerTelegramUserId, targetType, targetId, amountUzs, PaymentProvider.Click, expiresAtUtc);
    }

    public static PaymentOrder CreateContactUnlockWithCredits(long payerTelegramUserId, ContactTargetType targetType, Guid targetId)
    {
        var order = new PaymentOrder(payerTelegramUserId, targetType, targetId, 1, PaymentProvider.Credit);
        order.MarkPaid($"credit-{order.Id:N}");
        return order;
    }

    public void MarkPaid(string providerTransactionId)
    {
        if (string.IsNullOrWhiteSpace(providerTransactionId))
        {
            throw new ArgumentException("Provider transaction id is required.", nameof(providerTransactionId));
        }

        if (Status == PaymentOrderStatus.Paid)
        {
            if (ProviderTransactionId != providerTransactionId)
            {
                throw new InvalidOperationException("Payment order was already completed by another transaction.");
            }

            return;
        }

        if (ExpireIfOverdue(DateTime.UtcNow) || Status != PaymentOrderStatus.Pending)
        {
            throw new InvalidOperationException("Only pending payment orders can be paid.");
        }

        ProviderTransactionId = providerTransactionId;
        Status = PaymentOrderStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
    }

    public bool ExpireIfOverdue(DateTime utcNow)
    {
        if (Status != PaymentOrderStatus.Pending || ExpiresAtUtc > utcNow)
        {
            return false;
        }

        Status = PaymentOrderStatus.Expired;
        return true;
    }
}
