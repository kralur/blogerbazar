using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using MediatR;

namespace BloggerBazar.Application.Features.Bloggers;

public sealed record GetMyBloggerProfileQuery(long TelegramUserId) : IRequest<MyBloggerProfileDto?>;

public sealed record MyBloggerProfileDto(
    Guid Id, string Name, string? LastName, string? Username, string City, IReadOnlyCollection<string> Categories,
    string? Bio, string? AvatarUrl, string? Phone, string? Email, int TotalFollowers, int? AverageReach,
    decimal? EngagementRate, int? StoriesPrice, int? ReelsPrice, int? PostPrice, int? IntegrationPrice,
    bool BarterEnabled, int Status, IReadOnlyCollection<PortfolioItemDto> PortfolioItems, IReadOnlyCollection<SocialPlatformDto> Platforms)
{
    public static MyBloggerProfileDto From(BloggerProfile profile) => new(
        profile.Id, profile.Name, profile.LastName, profile.Username, profile.City, profile.Categories, profile.Bio,
        profile.AvatarUrl, profile.Phone, profile.Email, profile.TotalFollowers, profile.AverageReach, profile.EngagementRate,
        profile.StoriesPrice, profile.ReelsPrice, profile.PostPrice, profile.IntegrationPrice, profile.BarterEnabled,
        (int)profile.Status, profile.PortfolioItems.Select(PortfolioItemDto.From).ToArray(), profile.Platforms.Select(SocialPlatformDto.From).ToArray());
}

public sealed class GetMyBloggerProfileHandler(IMarketplaceCatalogReadModel catalog)
    : IRequestHandler<GetMyBloggerProfileQuery, MyBloggerProfileDto?>
{
    public async Task<MyBloggerProfileDto?> Handle(GetMyBloggerProfileQuery query, CancellationToken cancellationToken)
    {
        return await catalog.GetMyBloggerAsync(query.TelegramUserId, cancellationToken);
    }
}
