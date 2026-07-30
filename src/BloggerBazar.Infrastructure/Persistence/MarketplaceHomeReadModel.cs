using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.Marketplace;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class MarketplaceHomeReadModel(BloggerBazarDbContext dbContext) : IMarketplaceHomeReadModel
{
    private const int RailSize = 8;

    public async Task<MarketplaceHomeDto> GetAsync(CancellationToken cancellationToken)
    {
        var approvedBloggers = dbContext.BloggerProfiles.AsNoTracking()
            .Where(profile => profile.Status == BloggerStatus.Approved);
        var publishedCampaigns = dbContext.Campaigns.AsNoTracking()
            .Where(campaign => campaign.Status == CampaignStatus.Published);
        var businesses = dbContext.BusinessProfiles.AsNoTracking();
        var brandFaces = dbContext.BrandFaceProfiles.AsNoTracking();

        var promotedBloggers = await ProjectBloggers(approvedBloggers
                .Where(profile => profile.IsPromoted)
                .OrderByDescending(profile => profile.UpdatedAtUtc)
                .Take(RailSize))
            .ToArrayAsync(cancellationToken);

        var promotedCampaigns = await publishedCampaigns
            .Where(campaign => campaign.IsPromoted)
            .OrderByDescending(campaign => campaign.UpdatedAtUtc)
            .Take(RailSize)
            .Select(campaign => new CampaignDto(
                campaign.Id,
                campaign.BusinessId,
                campaign.Business.Name,
                campaign.Title,
                campaign.Description,
                campaign.City,
                campaign.Categories,
                campaign.Requirements,
                campaign.BudgetFrom,
                campaign.BudgetTo,
                campaign.Deadline,
                campaign.IsPromoted,
                (int)campaign.Status,
                campaign.Applications.Count,
                campaign.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        var topRatedBloggers = await ProjectBloggers(approvedBloggers
                .OrderByDescending(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed))
                .ThenByDescending(profile => profile.Reviews.Select(review => (decimal?)review.Rating).Average() ?? 0)
                .ThenByDescending(profile => profile.Reviews.Count)
                .Take(RailSize))
            .ToArrayAsync(cancellationToken);

        var newBloggers = await ProjectBloggers(approvedBloggers
                .OrderByDescending(profile => profile.CreatedAtUtc)
                .Take(RailSize))
            .ToArrayAsync(cancellationToken);

        var newBrandFaces = await brandFaces
            .OrderByDescending(profile => profile.IsPromoted)
            .ThenByDescending(profile => profile.UpdatedAtUtc)
            .Take(RailSize)
            .Select(profile => new BrandFacePublicDto(
                profile.Id,
                profile.Name,
                profile.City,
                profile.Languages,
                profile.Categories,
                profile.Experience,
                profile.Instagram,
                profile.PortfolioUrl,
                profile.CollaborationPrice,
                profile.Description,
                profile.AvatarUrl,
                profile.IsPromoted,
                profile.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        var popularBusinesses = await businesses
            .OrderByDescending(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed))
            .ThenByDescending(profile => profile.Reviews.Select(review => (decimal?)review.Rating).Average() ?? 0)
            .ThenByDescending(profile => profile.Campaigns.Count)
            .Take(RailSize)
            .Select(profile => new MarketplaceBusinessDto(
                profile.Id,
                profile.Name,
                profile.City,
                profile.LogoUrl,
                profile.Campaigns.Count,
                profile.Deals.Count(deal => deal.Status == DealStatus.Completed),
                profile.Reviews.Select(review => (decimal?)review.Rating).Average()))
            .ToArrayAsync(cancellationToken);

        var categories = await approvedBloggers.SelectMany(profile => profile.Categories)
            .Concat(brandFaces.SelectMany(profile => profile.Categories))
            .Concat(publishedCampaigns.SelectMany(campaign => campaign.Categories))
            .Distinct()
            .OrderBy(category => category)
            .ToArrayAsync(cancellationToken);

        var averageRating = await approvedBloggers
            .SelectMany(profile => profile.Reviews)
            .Select(review => (decimal?)review.Rating)
            .AverageAsync(cancellationToken);

        var statistics = new MarketplaceStatisticsDto(
            await approvedBloggers.CountAsync(cancellationToken),
            await businesses.CountAsync(cancellationToken),
            await publishedCampaigns.CountAsync(cancellationToken),
            await dbContext.Deals.AsNoTracking()
                .CountAsync(deal => deal.Status == DealStatus.Completed && deal.Blogger.Status == BloggerStatus.Approved, cancellationToken),
            averageRating.HasValue ? decimal.Round(averageRating.Value, 1) : null);

        return new MarketplaceHomeDto(
            promotedBloggers,
            promotedCampaigns,
            topRatedBloggers,
            newBloggers,
            newBrandFaces,
            popularBusinesses,
            categories,
            statistics);
    }

    private static IQueryable<BloggerProfileDto> ProjectBloggers(IQueryable<BloggerProfile> profiles) =>
        profiles.Select(profile => new BloggerProfileDto(
            profile.Id,
            profile.Name,
            profile.City,
            profile.Categories,
            profile.Bio,
            profile.AvatarUrl,
            profile.CoverUrl,
            profile.TotalFollowers,
            profile.TotalFollowers < 5_000 ? (int)CreatorLevel.Newcomer
                : profile.TotalFollowers < 50_000 ? (int)CreatorLevel.Microblogger
                : profile.TotalFollowers < 500_000 ? (int)CreatorLevel.Blogger
                : (int)CreatorLevel.TopBlogger,
            profile.AverageReach,
            profile.EngagementRate,
            profile.StoriesPrice,
            profile.ReelsPrice,
            profile.PostPrice,
            profile.IntegrationPrice,
            profile.BarterEnabled,
            profile.IsVerified,
            profile.IsPromoted,
            (int)profile.Status,
            profile.Reviews.Select(review => (decimal?)review.Rating).Average(),
            profile.Reviews.Count,
            profile.Deals.Count(deal => deal.Status == DealStatus.Completed),
            profile.PortfolioItems.Select(item => new PortfolioItemDto(item.Id, item.Title, (int)item.Type, item.Url)).ToArray(),
            profile.Platforms.Select(platform => new SocialPlatformDto(platform.Id, platform.Type, platform.Url, platform.Followers, platform.ScreenshotUrl)).ToArray()));
}
