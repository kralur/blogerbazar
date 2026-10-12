using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Tips;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BotTipRepository(BloggerBazarDbContext dbContext) : IBotTipRepository
{
    public async Task<IReadOnlyList<BotTipRecipient>> GetDueAsync(DateTime dayStartUtc, int take, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers.AsNoTracking()
            .Where(user => !user.IsBlocked && !user.IsDeleted && !user.TipsDisabled && user.CreatedAtUtc < dayStartUtc
                && (user.LastTipSentAtUtc == null || user.LastTipSentAtUtc < dayStartUtc))
            .OrderBy(user => user.TelegramUserId)
            .Take(take)
            .Select(user => new BotTipRecipient(user.TelegramUserId, user.SelectedMarketplaceRole, user.LastTipNumber))
            .ToArrayAsync(cancellationToken);

    public async Task<bool> TryClaimAsync(long telegramUserId, int? tipNumber, DateTime dayStartUtc, DateTime nowUtc, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers
            .Where(user => user.TelegramUserId == telegramUserId && (user.LastTipSentAtUtc == null || user.LastTipSentAtUtc < dayStartUtc))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.LastTipSentAtUtc, nowUtc)
                .SetProperty(user => user.LastTipNumber, tipNumber), cancellationToken) == 1;
}
