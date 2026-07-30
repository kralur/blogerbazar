using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BloggerProfileRepository(BloggerBazarDbContext dbContext) : IBloggerProfileRepository
{
    public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BloggerProfiles.SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BloggerProfiles.SingleOrDefaultAsync(profile => profile.TelegramUserId == telegramUserId, cancellationToken);

    public Task<BloggerProfile?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.BloggerProfiles.SingleOrDefaultAsync(profile => profile.Username == username, cancellationToken);

    public async Task<IReadOnlyList<BloggerProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BloggerProfiles.AsNoTracking().Where(profile => profile.Status == BloggerStatus.Pending)
            .OrderBy(profile => profile.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BloggerProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BloggerProfiles.AsNoTracking()
            .OrderByDescending(profile => profile.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.BloggerProfiles.AsNoTracking()
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

    public async Task<BloggerCatalogPage> SearchApprovedPageAsync(BloggerCatalogSearch search, CancellationToken cancellationToken)
    {
        var query = dbContext.BloggerProfiles.AsNoTracking().Where(profile => profile.Status == BloggerStatus.Approved);
        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var pattern = PostgresSearchPattern.Contains(search.Query.Trim());
            query = query.Where(profile => EF.Functions.ILike(profile.Name, pattern)
                || EF.Functions.ILike(profile.City, pattern)
                || profile.Categories.Any(category => EF.Functions.ILike(category, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(search.City))
        {
            var city = search.City.Trim();
            query = query.Where(profile => profile.City == city);
        }
        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            var category = search.Category.Trim();
            query = query.Where(profile => profile.Categories.Contains(category));
        }
        if (!string.IsNullOrWhiteSpace(search.Platform))
        {
            var platform = search.Platform.Trim();
            query = query.Where(profile => profile.Platforms.Any(item => item.Type == platform));
        }
        if (search.MinFollowers.HasValue) query = query.Where(profile => profile.TotalFollowers >= search.MinFollowers.Value);
        if (search.MinEngagementRate.HasValue) query = query.Where(profile => profile.EngagementRate >= search.MinEngagementRate.Value);
        if (search.MaxEngagementRate.HasValue) query = query.Where(profile => profile.EngagementRate <= search.MaxEngagementRate.Value);
        if (search.MinPrice.HasValue)
        {
            query = query.Where(profile => (profile.StoriesPrice != null && profile.StoriesPrice >= search.MinPrice)
                || (profile.ReelsPrice != null && profile.ReelsPrice >= search.MinPrice)
                || (profile.PostPrice != null && profile.PostPrice >= search.MinPrice)
                || (profile.IntegrationPrice != null && profile.IntegrationPrice >= search.MinPrice));
        }
        if (search.MaxPrice.HasValue)
        {
            query = query.Where(profile => (profile.StoriesPrice != null && profile.StoriesPrice <= search.MaxPrice)
                || (profile.ReelsPrice != null && profile.ReelsPrice <= search.MaxPrice)
                || (profile.PostPrice != null && profile.PostPrice <= search.MaxPrice)
                || (profile.IntegrationPrice != null && profile.IntegrationPrice <= search.MaxPrice));
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = search.Sort switch
        {
            "rating" => query.OrderByDescending(profile => profile.Reviews.Average(review => (decimal?)review.Rating) ?? 0).ThenByDescending(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed)).ThenByDescending(profile => profile.Reviews.Count),
            "er" => query.OrderByDescending(profile => profile.EngagementRate ?? 0).ThenByDescending(profile => profile.TotalFollowers),
            "price" => query.OrderBy(profile => profile.StoriesPrice ?? profile.ReelsPrice ?? profile.PostPrice ?? profile.IntegrationPrice ?? int.MaxValue).ThenByDescending(profile => profile.TotalFollowers),
            "newest" => query.OrderByDescending(profile => profile.CreatedAtUtc),
            _ => query.OrderByDescending(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed)).ThenByDescending(profile => profile.Reviews.Average(review => (decimal?)review.Rating) ?? 0).ThenByDescending(profile => profile.TotalFollowers)
        };

        var profiles = await ordered
            .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).ToListAsync(cancellationToken);
        return new BloggerCatalogPage(profiles, total);
    }

    public async Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => await dbContext.BloggerProfiles.AddAsync(profile, cancellationToken);
}
