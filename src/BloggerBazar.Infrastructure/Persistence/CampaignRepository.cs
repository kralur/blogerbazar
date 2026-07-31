using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CampaignRepository(BloggerBazarDbContext dbContext) : ICampaignRepository
{
    public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Campaigns.Include(campaign => campaign.Business)
            .SingleOrDefaultAsync(campaign => campaign.Id == id && !campaign.Business.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.Campaigns.AsNoTracking().Where(campaign => campaign.Status == CampaignStatus.Published && !campaign.Business.IsDeleted);
        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(campaign => campaign.City == city.Trim());
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(campaign => campaign.Categories.Contains(category.Trim()));
        }

        return await query.OrderByDescending(campaign => campaign.IsPromoted)
            .ThenByDescending(campaign => campaign.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Campaign>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.Campaigns.AsNoTracking()
            .OrderByDescending(campaign => campaign.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task AddAsync(Campaign campaign, CancellationToken cancellationToken) => await dbContext.Campaigns.AddAsync(campaign, cancellationToken);
}
