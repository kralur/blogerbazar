namespace BloggerBazar.Domain.Entities;

public sealed class CreditAccount
{
    private CreditAccount() { }

    private CreditAccount(long telegramUserId)
    {
        Id = Guid.NewGuid();
        TelegramUserId = telegramUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public long TelegramUserId { get; private set; }
    public int Balance { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<CreditLedgerEntry> LedgerEntries { get; private set; } = new List<CreditLedgerEntry>();

    public static CreditAccount Create(long telegramUserId) => new(telegramUserId);

    public void Apply(int amount)
    {
        if (Balance + amount < 0)
        {
            throw new InvalidOperationException("Insufficient credits.");
        }

        Balance += amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
