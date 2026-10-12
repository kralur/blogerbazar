using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Marketplace;

// D53 "For you": a creator sees open campaigns in its categories or city; a business sees creators for the categories
// and cities of its open campaigns. A plain match on category and city, no ML; nothing when there is nothing to match.
public static class RecommendationAudiences
{
    public const string Creator = "creator";
    public const string Business = "business";
    public const string None = "none";
}

public sealed record RecommendationsDto(
    string Audience,
    IReadOnlyList<CampaignDto> Campaigns,
    IReadOnlyList<BloggerProfileDto> Bloggers,
    IReadOnlyList<BrandFacePublicDto> BrandFaces)
{
    public static RecommendationsDto Empty(string audience) => new(audience, [], [], []);
}

public sealed record GetRecommendationsQuery(long TelegramUserId) : IRequest<RecommendationsDto>;

public sealed class GetRecommendationsHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBrandFaceProfileRepository brandFaces,
    IBusinessProfileRepository businesses,
    IMarketplaceRecommendationReadModel recommendations) : IRequestHandler<GetRecommendationsQuery, RecommendationsDto>
{
    public const int Take = 8;

    public async Task<RecommendationsDto> Handle(GetRecommendationsQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted)
        {
            return RecommendationsDto.Empty(RecommendationAudiences.None);
        }

        // Only the selected role's profile (selected-role rule); no fallback to another profile.
        switch (user.SelectedMarketplaceRole)
        {
            case MarketplaceRole.Blogger when await bloggers.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken) is { IsDeleted: false } blogger:
                return new RecommendationsDto(RecommendationAudiences.Creator,
                    await recommendations.CampaignsForCreatorAsync(MarketplaceRole.Blogger, blogger.Id, blogger.Categories.ToArray(), blogger.City, Take, cancellationToken), [], []);
            case MarketplaceRole.BrandFace when await brandFaces.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken) is { IsDeleted: false } brandFace:
                return new RecommendationsDto(RecommendationAudiences.Creator,
                    await recommendations.CampaignsForCreatorAsync(MarketplaceRole.BrandFace, brandFace.Id, brandFace.Categories.ToArray(), brandFace.City, Take, cancellationToken), [], []);
            case MarketplaceRole.Business when await businesses.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken) is { } business:
                var (matchedBloggers, matchedBrandFaces) = await recommendations.CreatorsForBusinessAsync(business.Id, Take, cancellationToken);
                return new RecommendationsDto(RecommendationAudiences.Business, [], matchedBloggers, matchedBrandFaces);
            default:
                return RecommendationsDto.Empty(RecommendationAudiences.None);
        }
    }
}
