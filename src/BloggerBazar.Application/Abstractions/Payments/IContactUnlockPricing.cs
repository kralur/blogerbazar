namespace BloggerBazar.Application.Abstractions.Payments;

public interface IContactUnlockPricing
{
    int AmountUzs { get; }
    TimeSpan PendingOrderLifetime { get; }
}
