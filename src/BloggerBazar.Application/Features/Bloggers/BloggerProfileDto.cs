using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Features.Bloggers;

public sealed record BloggerProfileDto(
    Guid Id,
    string Name,
    string? Username,
    string City,
    IReadOnlyCollection<string> Categories,
    string? Bio,
    string? AvatarUrl,
    string? CoverUrl,
    int TotalFollowers,
    int? AverageReach,
    decimal? EngagementRate,
    int? StoriesPrice,
    int? ReelsPrice,
    int? PostPrice,
    int? IntegrationPrice,
    bool BarterEnabled,
    bool IsVerified,
    bool IsPromoted,
    int Status,
    decimal? Rating,
    int ReviewsCount,
    int CompletedDealsCount,
    IReadOnlyCollection<PortfolioItemDto> PortfolioItems,
    IReadOnlyCollection<SocialPlatformDto> Platforms)
{
    public static BloggerProfileDto From(BloggerProfile profile) => new(
        profile.Id,
        profile.Name,
        profile.Username,
        profile.City,
        profile.Categories,
        profile.Bio,
        profile.AvatarUrl,
        profile.CoverUrl,
        profile.TotalFollowers,
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
        profile.Reviews.Count == 0 ? null : profile.Reviews.Select(review => (decimal)review.Rating).Average(),
        profile.Reviews.Count,
        profile.Deals.Count(deal => deal.Status == DealStatus.Completed),
        profile.PortfolioItems.Select(PortfolioItemDto.From).ToArray(),
        profile.Platforms.Select(SocialPlatformDto.From).ToArray());
}

public sealed record PortfolioItemDto(Guid Id, string Title, int Type, string Url)
{
    public static PortfolioItemDto From(PortfolioItem item) => new(item.Id, item.Title, (int)item.Type, item.Url);
}

public sealed record SocialPlatformDto(Guid Id, string Type, string Url, int? Followers, string? ScreenshotUrl)
{
    public static SocialPlatformDto From(SocialPlatform platform) =>
        new(platform.Id, platform.Type, platform.Url, platform.Followers, platform.ScreenshotUrl);
}
