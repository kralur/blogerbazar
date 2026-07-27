using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CreditAccountRepository(BloggerBazarDbContext dbContext) : ICreditAccountRepository
{
    public Task<CreditAccount?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.CreditAccounts.SingleOrDefaultAsync(account => account.TelegramUserId == telegramUserId, cancellationToken);

    public async Task AddAsync(CreditAccount account, CancellationToken cancellationToken) =>
        await dbContext.CreditAccounts.AddAsync(account, cancellationToken);

    public async Task AddEntryAsync(CreditLedgerEntry entry, CancellationToken cancellationToken) =>
        await dbContext.CreditLedgerEntries.AddAsync(entry, cancellationToken);
}
