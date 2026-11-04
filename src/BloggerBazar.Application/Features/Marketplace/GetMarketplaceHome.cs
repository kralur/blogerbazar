using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.BrandFaces;
using MediatR;

namespace BloggerBazar.Application.Features.Marketplace;

public sealed record MarketplaceStatisticsDto(int ApprovedBloggers, int Companies, int ActiveCampaigns, int CompletedDeals, decimal? AverageRating);

public sealed record MarketplaceBusinessDto(Guid Id, string Name, string? City, string? LogoUrl, int CampaignsCount, int CompletedDealsCount, decimal? Rating);

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

// Pre-launch audit 2026-11-04: the same answer for everyone, yet every app open ran its twelve queries (including
// sorting all bloggers by deals and rating). Cached for a minute under the catalog versions, like the catalog pages,
// so a change to any profile or campaign shows up as soon as it rotates them.
public sealed class GetMarketplaceHomeHandler(IMarketplaceHomeReadModel home, ICatalogCache? cache = null) : IRequestHandler<GetMarketplaceHomeQuery, MarketplaceHomeDto>
{
    public static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(1);

    public async Task<MarketplaceHomeDto> Handle(GetMarketplaceHomeQuery query, CancellationToken cancellationToken)
    {
        if (cache is null) return await home.GetAsync(cancellationToken);
        var key = $"marketplace-home:v1:{await cache.GetNamespaceVersionAsync(cancellationToken)}:{await cache.GetNamespaceVersionAsync(CampaignCatalogCache.Namespace, cancellationToken)}";
        if (await cache.GetAsync<MarketplaceHomeDto>(key, cancellationToken) is { } cached) return cached;
        var fresh = await home.GetAsync(cancellationToken);
        await cache.SetAsync(key, fresh, CacheTime, cancellationToken);
        return fresh;
    }
}
