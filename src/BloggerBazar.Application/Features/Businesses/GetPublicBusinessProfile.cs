using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;
using MediatR;

namespace BloggerBazar.Application.Features.Businesses;

// What a blogger sees before working with a business. Contacts stay private until a deal exists (D31).
public sealed record GetPublicBusinessProfileQuery(Guid BusinessId) : IRequest<PublicBusinessProfileDto?>;

public sealed record PublicBusinessCampaignDto(Guid Id, string Title, string? City, int? BudgetFrom, int? BudgetTo, DateTime? Deadline);

public sealed record PublicBusinessProfileDto(
    Guid Id,
    string Name,
    string? City,
    string? LogoUrl,
    string? WebsiteUrl,
    string? Description,
    bool IsVerified,
    int CompletedDealsCount,
    DateTime CreatedAtUtc,
    IReadOnlyList<PublicBusinessCampaignDto> OpenCampaigns,
    decimal? Rating = null,
    int ReviewsCount = 0,
    IReadOnlyList<ReviewDto>? Reviews = null);

public sealed class GetPublicBusinessProfileHandler(IPublicBusinessReadModel businesses, IReviewReadModel reviews)
    : IRequestHandler<GetPublicBusinessProfileQuery, PublicBusinessProfileDto?>
{
    private const int ReviewsShown = 10;

    public async Task<PublicBusinessProfileDto?> Handle(GetPublicBusinessProfileQuery query, CancellationToken cancellationToken)
    {
        var profile = await businesses.GetAsync(query.BusinessId, DateTime.UtcNow, cancellationToken);
        if (profile is null) return null;
        var businessReviews = await reviews.GetBusinessReviewsAsync(query.BusinessId, ReviewsShown, cancellationToken);
        return profile with { Rating = businessReviews.Rating, ReviewsCount = businessReviews.ReviewsCount, Reviews = businessReviews.Items };
    }
}
