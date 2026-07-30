using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Marketplace;

public sealed record MarketplaceStatisticsDto(int ApprovedBloggers, int Companies, int ActiveCampaigns, int CompletedDeals, decimal? AverageRating);

public sealed record MarketplaceBusinessDto(Guid Id, string Name, string? City, string? LogoUrl, int CampaignsCount, int CompletedDealsCount, decimal? Rating)
{
    public static MarketplaceBusinessDto From(BusinessProfile profile)
    {
        var ratings = profile.Reviews.Select(review => (decimal)review.Rating).ToArray();
        return new(profile.Id, profile.Name, profile.City, profile.LogoUrl, profile.Campaigns.Count, profile.Deals.Count(deal => deal.Status == DealStatus.Completed), ratings.Length == 0 ? null : decimal.Round(ratings.Average(), 1));
    }
}

public sealed record MarketplaceHomeDto(
    IReadOnlyList<BloggerProfileDto> PromotedBloggers,
    IReadOnlyList<CampaignDto> PromotedCampaigns,
    IReadOnlyList<BloggerProfileDto> TopRatedBloggers,
    IReadOnlyList<BloggerProfileDto> NewBloggers,
    IReadOnlyList<BrandFacePublicDto> NewBrandFaces,
    IReadOnlyList<MarketplaceBusinessDto> PopularBusinesses,
    IReadOnlyList<string> Categories,
    MarketplaceStatisticsDto Statistics);

public sealed record GetMarketplaceHomeQuery : IRequest<MarketplaceHomeDto>;

public sealed class GetMarketplaceHomeHandler(
    IBloggerProfileRepository bloggers,
    IBrandFaceProfileRepository brandFaces,
    IBusinessProfileRepository businesses,
    ICampaignRepository campaigns) : IRequestHandler<GetMarketplaceHomeQuery, MarketplaceHomeDto>
{
    public async Task<MarketplaceHomeDto> Handle(GetMarketplaceHomeQuery query, CancellationToken cancellationToken)
    {
        var bloggerProfiles = await bloggers.GetAllAsync(500, cancellationToken);
        var approved = bloggerProfiles.Where(profile => profile.Status == BloggerStatus.Approved).ToArray();
        var allCampaigns = await campaigns.GetAllAsync(500, cancellationToken);
        var allBusinesses = await businesses.GetAllAsync(500, cancellationToken);
        var allBrandFaces = await brandFaces.GetAllAsync(500, cancellationToken);
        var completedDeals = approved.Sum(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed));
        var ratings = approved.SelectMany(profile => profile.Reviews).Select(review => (decimal)review.Rating).ToArray();

        return new MarketplaceHomeDto(
            approved.Where(profile => profile.IsPromoted).OrderByDescending(profile => profile.UpdatedAtUtc).Take(8).Select(BloggerProfileDto.From).ToArray(),
            allCampaigns.Where(campaign => campaign.Status == CampaignStatus.Published && campaign.IsPromoted).OrderByDescending(campaign => campaign.UpdatedAtUtc).Take(8).Select(campaign => CampaignDto.From(campaign, campaign.Business.Name)).ToArray(),
            approved.OrderByDescending(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed)).ThenByDescending(profile => profile.Reviews.Count == 0 ? 0 : profile.Reviews.Average(review => review.Rating)).ThenByDescending(profile => profile.Reviews.Count).Take(8).Select(BloggerProfileDto.From).ToArray(),
            approved.OrderByDescending(profile => profile.CreatedAtUtc).Take(8).Select(BloggerProfileDto.From).ToArray(),
            allBrandFaces.Take(8).Select(BrandFacePublicDto.From).ToArray(),
            allBusinesses.OrderByDescending(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed)).ThenByDescending(profile => profile.Reviews.Count == 0 ? 0 : profile.Reviews.Average(review => review.Rating)).ThenByDescending(profile => profile.Campaigns.Count).Take(8).Select(MarketplaceBusinessDto.From).ToArray(),
            approved.SelectMany(profile => profile.Categories).Concat(allBrandFaces.SelectMany(profile => profile.Categories)).Concat(allCampaigns.SelectMany(campaign => campaign.Categories)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(category => category).ToArray(),
            new MarketplaceStatisticsDto(approved.Length, allBusinesses.Count, allCampaigns.Count(campaign => campaign.Status == CampaignStatus.Published), completedDeals, ratings.Length == 0 ? null : decimal.Round(ratings.Average(), 1)));
    }
}
