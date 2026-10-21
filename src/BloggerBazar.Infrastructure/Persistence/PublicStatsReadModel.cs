using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.PublicStats;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class PublicStatsReadModel(BloggerBazarDbContext dbContext) : IPublicStatsReadModel
{
    // One DbContext runs one query at a time, so the three counts go in sequence.
    public async Task<PublicStatsDto> GetAsync(CancellationToken cancellationToken)
    {
        var bloggers = await dbContext.BloggerProfiles.AsNoTracking()
            .CountAsync(profile => !profile.IsDeleted && profile.Status == BloggerStatus.Approved
                && !dbContext.PlatformUsers.Any(user => user.TelegramUserId == profile.TelegramUserId && (user.IsBlocked || user.IsDeleted)), cancellationToken);
        var businesses = await dbContext.BusinessProfiles.AsNoTracking()
            .CountAsync(profile => !profile.IsDeleted && profile.ModerationStatus == BloggerStatus.Approved
                && !dbContext.PlatformUsers.Any(user => user.TelegramUserId == profile.TelegramUserId && (user.IsBlocked || user.IsDeleted)), cancellationToken);
        var completedDeals = await dbContext.Deals.AsNoTracking().CountAsync(deal => deal.Status == DealStatus.Completed, cancellationToken);
        return new PublicStatsDto(bloggers, businesses, completedDeals);
    }
}
