using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICreditAccountRepository
{
    Task<CreditAccount?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task AddAsync(CreditAccount account, CancellationToken cancellationToken);
    Task AddEntryAsync(CreditLedgerEntry entry, CancellationToken cancellationToken);
}
