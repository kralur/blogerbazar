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

    public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) =>
        dbContext.Campaigns.Include(campaign => campaign.Business)
            .SingleOrDefaultAsync(campaign => campaign.Id == id && campaign.BusinessId == businessId && !campaign.Business.IsDeleted, cancellationToken);

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

    public async Task<bool> DeleteWithoutApplicationsAsync(Guid id, Guid businessId, CancellationToken cancellationToken) =>
        await dbContext.Campaigns
            .Where(campaign => campaign.Id == id && campaign.BusinessId == businessId && !campaign.Applications.Any())
            .ExecuteDeleteAsync(cancellationToken) == 1;

    public Task<int> CountCreatedSinceAsync(Guid businessId, DateTime sinceUtc, CancellationToken cancellationToken) =>
        dbContext.Campaigns.CountAsync(campaign => campaign.BusinessId == businessId && campaign.CreatedAtUtc >= sinceUtc, cancellationToken);

    public async Task<IReadOnlyList<DueScheduledCampaign>> ListDueScheduledAsync(DateTime nowUtc, int take, CancellationToken cancellationToken) =>
        await dbContext.Campaigns.AsNoTracking()
            .Where(campaign => campaign.ScheduledPublishAtUtc != null && campaign.ScheduledPublishAtUtc <= nowUtc
                && (campaign.Status == CampaignStatus.Draft || campaign.Status == CampaignStatus.Archived)
                && !campaign.Business.IsDeleted)
            .OrderBy(campaign => campaign.ScheduledPublishAtUtc)
            .Take(take)
            .Select(campaign => new DueScheduledCampaign(campaign.Id, campaign.Title, campaign.ScheduledPublishAtUtc!.Value, campaign.Deadline, campaign.Business.TelegramUserId))
            .ToListAsync(cancellationToken);

    public async Task<bool> TryPublishScheduledAsync(Guid id, DateTime scheduledAtUtc, DateTime nowUtc, CancellationToken cancellationToken) =>
        await Unchanged(id, scheduledAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(campaign => campaign.Status, CampaignStatus.Published)
                .SetProperty(campaign => campaign.ScheduledPublishAtUtc, (DateTime?)null)
                .SetProperty(campaign => campaign.UpdatedAtUtc, nowUtc), cancellationToken) == 1;

    public async Task<bool> TryDropScheduleAsync(Guid id, DateTime scheduledAtUtc, DateTime nowUtc, CancellationToken cancellationToken) =>
        await Unchanged(id, scheduledAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(campaign => campaign.ScheduledPublishAtUtc, (DateTime?)null)
                .SetProperty(campaign => campaign.UpdatedAtUtc, nowUtc), cancellationToken) == 1;

    // The plan the job read: same time, still a draft or closed, company not deleted.
    private IQueryable<Campaign> Unchanged(Guid id, DateTime scheduledAtUtc) =>
        dbContext.Campaigns.Where(campaign => campaign.Id == id && campaign.ScheduledPublishAtUtc == scheduledAtUtc
            && (campaign.Status == CampaignStatus.Draft || campaign.Status == CampaignStatus.Archived)
            && !campaign.Business.IsDeleted);

    public async Task AddAsync(Campaign campaign, CancellationToken cancellationToken) => await dbContext.Campaigns.AddAsync(campaign, cancellationToken);
}
