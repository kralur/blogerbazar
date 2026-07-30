using BloggerBazar.Application.Abstractions.Payments;
using Microsoft.Extensions.Configuration;

namespace BloggerBazar.Infrastructure.Payments;

internal sealed class ContactUnlockPricing(IConfiguration configuration) : IContactUnlockPricing
{
    public int AmountUzs { get; } = configuration.GetValue<int?>("Payments:ContactUnlockAmountUzs")
        ?? throw new InvalidOperationException("Payments:ContactUnlockAmountUzs must be configured.");

    public TimeSpan PendingOrderLifetime { get; } = TimeSpan.FromMinutes(
        configuration.GetValue<int?>("Payments:ContactUnlockExpiresInMinutes")
        ?? throw new InvalidOperationException("Payments:ContactUnlockExpiresInMinutes must be configured."));
}
