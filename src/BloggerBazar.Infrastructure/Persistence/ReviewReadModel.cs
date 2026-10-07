using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

// Only published reviews are visible here: the Review query filter hides blind reviews.
internal sealed class ReviewReadModel(BloggerBazarDbContext dbContext) : IReviewReadModel
{
    public async Task<IReadOnlyList<ReviewDto>> GetBloggerReviewsAsync(Guid bloggerId, int take, CancellationToken cancellationToken) =>
        await Latest(dbContext.Reviews.AsNoTracking().Where(review => review.BloggerId == bloggerId
                && review.TargetType == ReviewTargetType.Blogger
                && !review.Deal.Blogger.IsDeleted && !review.Deal.Business.IsDeleted), take)
            .ToArrayAsync(cancellationToken);

    public async Task<BusinessReviewsDto> GetBusinessReviewsAsync(Guid businessId, int take, CancellationToken cancellationToken)
    {
        var reviews = dbContext.Reviews.AsNoTracking().Where(review => review.BusinessId == businessId
            && review.TargetType == ReviewTargetType.Business
            && !review.Deal.Blogger.IsDeleted && !review.Deal.Business.IsDeleted);
        var count = await reviews.CountAsync(cancellationToken);
        var average = await reviews.AverageAsync(review => (decimal?)review.Rating, cancellationToken);
        var items = await Latest(reviews, take).ToArrayAsync(cancellationToken);
        return new BusinessReviewsDto(average.HasValue ? decimal.Round(average.Value, 1) : null, count, items);
    }

    private static IQueryable<ReviewDto> Latest(IQueryable<Review> reviews, int take) =>
        reviews.OrderByDescending(review => review.CreatedAtUtc).ThenByDescending(review => review.Id).Take(take)
            .Select(review => new ReviewDto(review.Id, review.DealId, (int)review.TargetType, review.Rating, review.Comment,
                review.TargetType == ReviewTargetType.Blogger ? review.Deal.Business.Name : review.Deal.Blogger.Name, review.CreatedAtUtc,
                review.TargetType == ReviewTargetType.Blogger ? review.Deal.BusinessId : review.Deal.BloggerId,
                review.TargetType == ReviewTargetType.Blogger ? review.Deal.Business.LogoUrl : review.Deal.Blogger.AvatarUrl));
}
