using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BloggerProfileRepository(BloggerBazarDbContext dbContext) : IBloggerProfileRepository
{
    public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BloggerProfiles.Include(profile => profile.PortfolioItems).Include(profile => profile.Platforms).Include(profile => profile.Reviews).Include(profile => profile.Deals)
            .SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BloggerProfiles.Include(profile => profile.PortfolioItems).Include(profile => profile.Platforms).Include(profile => profile.Reviews).Include(profile => profile.Deals)
            .SingleOrDefaultAsync(profile => profile.TelegramUserId == telegramUserId, cancellationToken);

    public async Task<IReadOnlyList<BloggerProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BloggerProfiles.AsNoTracking().Where(profile => profile.Status == BloggerStatus.Pending)
            .OrderBy(profile => profile.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BloggerProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BloggerProfiles.AsNoTracking().Include(profile => profile.Platforms).Include(profile => profile.Reviews).Include(profile => profile.Deals)
            .OrderByDescending(profile => profile.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.BloggerProfiles.AsNoTracking().Include(profile => profile.Reviews).Include(profile => profile.Deals)
            .Where(profile => profile.Status == BloggerStatus.Approved);

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(profile => profile.City == city.Trim());
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(profile => profile.Categories.Contains(category.Trim()));
        }

        return await query.OrderByDescending(profile => profile.IsPromoted)
            .ThenByDescending(profile => profile.TotalFollowers)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => await dbContext.BloggerProfiles.AddAsync(profile, cancellationToken);
}
