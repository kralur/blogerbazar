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

public sealed class GetMarketplaceHomeHandler(IMarketplaceHomeReadModel home) : IRequestHandler<GetMarketplaceHomeQuery, MarketplaceHomeDto>
{
    public Task<MarketplaceHomeDto> Handle(GetMarketplaceHomeQuery query, CancellationToken cancellationToken) =>
        home.GetAsync(cancellationToken);
}
