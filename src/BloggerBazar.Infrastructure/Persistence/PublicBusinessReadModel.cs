using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class PublicBusinessReadModel(BloggerBazarDbContext dbContext) : IPublicBusinessReadModel
{
    private const int CampaignsShown = 10;

    public async Task<PublicBusinessProfileDto?> GetAsync(Guid businessId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var business = await dbContext.BusinessProfiles.AsNoTracking()
            .Where(profile => profile.Id == businessId && !profile.IsDeleted && profile.ModerationStatus == BloggerStatus.Approved
                && !dbContext.PlatformUsers.Any(user => user.TelegramUserId == profile.TelegramUserId && (user.IsBlocked || user.IsDeleted)))
            .Select(profile => new
            {
                profile.Id,
                profile.Name,
                profile.City,
                profile.LogoUrl,
                profile.WebsiteUrl,
                profile.Description,
                profile.IsVerified,
                profile.CreatedAtUtc,
                CompletedDeals = profile.Deals.Count(deal => deal.Status == DealStatus.Completed && !deal.Blogger.IsDeleted)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (business is null) return null;

        var openCampaigns = await MarketplaceCatalogVisibility.OpenForApplications(
                MarketplaceCatalogVisibility.PublicCampaigns(dbContext.Campaigns.AsNoTracking(), dbContext.BusinessProfiles.AsNoTracking(), dbContext.PlatformUsers.AsNoTracking()),
                utcNow)
            .Where(campaign => campaign.BusinessId == businessId)
            .OrderByDescending(campaign => campaign.CreatedAtUtc)
            .Take(CampaignsShown)
            .Select(campaign => new PublicBusinessCampaignDto(campaign.Id, campaign.Title, campaign.City, campaign.BudgetFrom, campaign.BudgetTo, campaign.Deadline))
            .ToArrayAsync(cancellationToken);

        return new PublicBusinessProfileDto(business.Id, business.Name, business.City, business.LogoUrl, business.WebsiteUrl, business.Description,
            business.IsVerified, business.CompletedDeals, business.CreatedAtUtc, openCampaigns);
    }
}
