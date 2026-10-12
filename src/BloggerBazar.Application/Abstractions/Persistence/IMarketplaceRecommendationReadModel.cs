using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IMarketplaceRecommendationReadModel
{
    Task<IReadOnlyList<CampaignDto>> CampaignsForCreatorAsync(MarketplaceRole creatorRole, Guid creatorId, IReadOnlyCollection<string> categories, string? city, int take, CancellationToken cancellationToken);
    Task<(IReadOnlyList<BloggerProfileDto> Bloggers, IReadOnlyList<BrandFacePublicDto> BrandFaces)> CreatorsForBusinessAsync(Guid businessId, int take, CancellationToken cancellationToken);
}
