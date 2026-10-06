using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

// A sent bot reminder; the unique (DealId, Kind, RecipientRole) row keeps each reminder to one delivery.
public sealed class DealReminder
{
    private DealReminder() { }

    public Guid Id { get; private set; }
    public Guid DealId { get; private set; }
    public DealReminderKind Kind { get; private set; }
    public MarketplaceRole RecipientRole { get; private set; }
    public DateTime SentAtUtc { get; private set; }
}
