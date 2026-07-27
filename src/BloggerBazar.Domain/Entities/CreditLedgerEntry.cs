using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class CreditLedgerEntry
{
    private CreditLedgerEntry() { }

    private CreditLedgerEntry(long telegramUserId, int amount, CreditSource source, string? note)
    {
        Id = Guid.NewGuid();
        TelegramUserId = telegramUserId;
        Amount = amount;
        Source = source;
        Note = note;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public long TelegramUserId { get; private set; }
    public CreditAccount Account { get; private set; } = null!;
    public int Amount { get; private set; }
    public CreditSource Source { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static CreditLedgerEntry Create(long telegramUserId, int amount, CreditSource source, string? note) => new(telegramUserId, amount, source, note);
}
