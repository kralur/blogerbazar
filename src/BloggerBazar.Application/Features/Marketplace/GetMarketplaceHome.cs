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

// The home page is the first and busiest request of every start, and its rails cost eleven queries. One answer serves
// everyone for a minute: a profile, photo or campaign change rotates a catalog namespace version (the same ones the
// catalog searches use), so the key changes and the next request reads the database again.
public sealed class GetMarketplaceHomeHandler(IMarketplaceHomeReadModel home, ICatalogCache cache) : IRequestHandler<GetMarketplaceHomeQuery, MarketplaceHomeDto>
{
    public static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(1);

    public async Task<MarketplaceHomeDto> Handle(GetMarketplaceHomeQuery query, CancellationToken cancellationToken)
    {
        var profilesVersion = await cache.GetNamespaceVersionAsync(cancellationToken);
        var campaignsVersion = await cache.GetNamespaceVersionAsync(CampaignCatalogCache.Namespace, cancellationToken);
        var key = $"marketplace:home:v1:{profilesVersion}:{campaignsVersion}";
        if (await cache.GetAsync<MarketplaceHomeDto>(key, cancellationToken) is { } cached)
        {
            return cached;
        }

        var fresh = await home.GetAsync(cancellationToken);
        await cache.SetAsync(key, fresh, CacheTime, cancellationToken);
        return fresh;
    }
}
