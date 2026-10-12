using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

// D53: a shared category weighs more than the same city; both together come first, promoted and newer next.
internal sealed class MarketplaceRecommendationReadModel(BloggerBazarDbContext dbContext) : IMarketplaceRecommendationReadModel
{
    public async Task<IReadOnlyList<CampaignDto>> CampaignsForCreatorAsync(MarketplaceRole creatorRole, Guid creatorId, IReadOnlyCollection<string> categories, string? city, int take, CancellationToken cancellationToken)
    {
        var wanted = categories.ToArray();
        if (wanted.Length == 0 && string.IsNullOrWhiteSpace(city)) return [];

        var campaigns = MarketplaceCatalogVisibility.OpenForApplications(
            MarketplaceCatalogVisibility.Listed(MarketplaceCatalogVisibility.PublicCampaigns(
                dbContext.Campaigns.AsNoTracking(), dbContext.BusinessProfiles.AsNoTracking(), dbContext.PlatformUsers.AsNoTracking())),
            DateTime.UtcNow);
        // Campaigns the creator already applied to are not news.
        campaigns = creatorRole == MarketplaceRole.BrandFace
            ? campaigns.Where(campaign => !campaign.Applications.Any(application => application.BrandFaceId == creatorId))
            : campaigns.Where(campaign => !campaign.Applications.Any(application => application.BloggerId == creatorId));

        var ranked = campaigns
            .Select(campaign => new
            {
                Campaign = campaign,
                Score = (campaign.Categories.Any(category => wanted.Contains(category)) ? 2 : 0) + (city != null && campaign.City == city ? 1 : 0)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Campaign.IsPromoted)
            .ThenByDescending(item => item.Campaign.CreatedAtUtc)
            .Select(item => item.Campaign)
            .Take(take);
        return await MarketplaceHomeReadModel.ProjectCampaigns(ranked).ToArrayAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<BloggerProfileDto> Bloggers, IReadOnlyList<BrandFacePublicDto> BrandFaces)> CreatorsForBusinessAsync(Guid businessId, int take, CancellationToken cancellationToken)
    {
        // What the business is looking for right now: the categories and cities of its open campaigns.
        var open = await MarketplaceCatalogVisibility.OpenForApplications(dbContext.Campaigns.AsNoTracking()
                .Where(campaign => campaign.BusinessId == businessId && campaign.Status == CampaignStatus.Published), DateTime.UtcNow)
            .Select(campaign => new { campaign.Categories, campaign.City })
            .ToArrayAsync(cancellationToken);
        var wanted = open.SelectMany(campaign => campaign.Categories).Distinct().ToArray();
        var cities = open.Select(campaign => campaign.City).OfType<string>().Distinct().ToArray();
        if (wanted.Length == 0 && cities.Length == 0) return ([], []);

        var bloggers = MarketplaceCatalogVisibility.Listed(MarketplaceCatalogVisibility.PublicBloggers(dbContext.BloggerProfiles.AsNoTracking()))
            .Where(profile => !dbContext.PlatformUsers.Any(user => user.TelegramUserId == profile.TelegramUserId && (user.IsBlocked || user.IsDeleted)))
            .Select(profile => new
            {
                Profile = profile,
                Score = (profile.Categories.Any(category => wanted.Contains(category)) ? 2 : 0) + (cities.Contains(profile.City) ? 1 : 0)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Profile.IsPromoted)
            .ThenByDescending(item => item.Profile.Deals.Count(deal => deal.Status == DealStatus.Completed))
            .ThenByDescending(item => item.Profile.CreatedAtUtc)
            .Select(item => item.Profile)
            .Take(take);

        var brandFaces = dbContext.BrandFaceProfiles.AsNoTracking()
            .Where(profile => !profile.IsDeleted && !profile.IsHidden)
            .Where(profile => !dbContext.PlatformUsers.Any(user => user.TelegramUserId == profile.TelegramUserId && (user.IsBlocked || user.IsDeleted)))
            .Select(profile => new
            {
                Profile = profile,
                Score = (profile.Categories.Any(category => wanted.Contains(category)) ? 2 : 0) + (cities.Contains(profile.City) ? 1 : 0)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Profile.IsPromoted)
            .ThenByDescending(item => item.Profile.UpdatedAtUtc)
            .Select(item => item.Profile)
            .Take(take);

        return (await MarketplaceHomeReadModel.ProjectBloggers(bloggers).ToArrayAsync(cancellationToken),
            await MarketplaceHomeReadModel.ProjectBrandFaces(brandFaces).ToArrayAsync(cancellationToken));
    }
}
