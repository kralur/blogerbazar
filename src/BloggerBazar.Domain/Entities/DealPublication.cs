namespace BloggerBazar.Domain.Entities;

// A link to a published ad within a deal (D51). Views are what the creator reported; the business may confirm them.
public sealed class DealPublication
{
    private DealPublication() { }

    private DealPublication(Guid dealId, string url, int? views, long createdByTelegramUserId, DateTime utcNow)
    {
        Id = Guid.NewGuid();
        DealId = dealId;
        Url = url;
        Views = views;
        CreatedByTelegramUserId = createdByTelegramUserId;
        CreatedAtUtc = utcNow;
        ViewsUpdatedAtUtc = views.HasValue ? utcNow : null;
    }

    public Guid Id { get; private set; }
    public Guid DealId { get; private set; }
    public Deal Deal { get; private set; } = null!;
    public string Url { get; private set; } = null!;
    public int? Views { get; private set; }
    public long CreatedByTelegramUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ViewsUpdatedAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public long? ConfirmedByTelegramUserId { get; private set; }
    public bool IsConfirmed => ConfirmedAtUtc.HasValue;

    public static DealPublication Create(Guid dealId, string url, int? views, long createdByTelegramUserId, DateTime utcNow) =>
        new(dealId, url, views, createdByTelegramUserId, utcNow);

    // New numbers need a new confirmation: the business confirmed the old ones, not these.
    public void UpdateViews(int? views, DateTime utcNow)
    {
        if (views == Views) return;
        Views = views;
        ViewsUpdatedAtUtc = utcNow;
        ConfirmedAtUtc = null;
        ConfirmedByTelegramUserId = null;
    }

    public void Confirm(long confirmedByTelegramUserId, DateTime utcNow)
    {
        if (IsConfirmed) return;
        ConfirmedAtUtc = utcNow;
        ConfirmedByTelegramUserId = confirmedByTelegramUserId;
    }
}
