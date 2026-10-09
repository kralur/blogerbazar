using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

// Only published reviews are visible here: the Review query filter hides blind reviews.
// A review stays after its author deletes the account (D43); the author then shows as deleted, without name or link.
internal sealed class ReviewReadModel(BloggerBazarDbContext dbContext) : IReviewReadModel
{
    public async Task<IReadOnlyList<ReviewDto>> GetBloggerReviewsAsync(Guid bloggerId, int skip, int take, CancellationToken cancellationToken) =>
        await Latest(dbContext.Reviews.AsNoTracking().Where(review => review.BloggerId == bloggerId
                && review.TargetType == ReviewTargetType.Blogger
                && !review.Deal.Blogger.IsDeleted), skip, take)
            .ToArrayAsync(cancellationToken);

    public async Task<BusinessReviewsDto> GetBusinessReviewsAsync(Guid businessId, int skip, int take, CancellationToken cancellationToken)
    {
        var reviews = dbContext.Reviews.AsNoTracking().Where(review => review.BusinessId == businessId
            && review.TargetType == ReviewTargetType.Business
            && !review.Deal.Business.IsDeleted);
        var count = await reviews.CountAsync(cancellationToken);
        var average = await reviews.AverageAsync(review => (decimal?)review.Rating, cancellationToken);
        var items = await Latest(reviews, skip, take).ToArrayAsync(cancellationToken);
        return new BusinessReviewsDto(average.HasValue ? decimal.Round(average.Value, 1) : null, count, items);
    }

    private static IQueryable<ReviewDto> Latest(IQueryable<Review> reviews, int skip, int take) =>
        reviews.OrderByDescending(review => review.CreatedAtUtc).ThenByDescending(review => review.Id).Skip(skip).Take(take)
            .Select(review => new
            {
                Review = review,
                AuthorDeleted = review.TargetType == ReviewTargetType.Blogger ? review.Deal.Business.IsDeleted : review.Deal.Blogger.IsDeleted
            })
            .Select(item => new ReviewDto(item.Review.Id, item.Review.DealId, (int)item.Review.TargetType, item.Review.Rating, item.Review.Comment,
                item.AuthorDeleted ? (string?)null : item.Review.TargetType == ReviewTargetType.Blogger ? item.Review.Deal.Business.Name : item.Review.Deal.Blogger.Name,
                item.Review.CreatedAtUtc,
                item.AuthorDeleted ? (Guid?)null : item.Review.TargetType == ReviewTargetType.Blogger ? item.Review.Deal.BusinessId : item.Review.Deal.BloggerId,
                item.AuthorDeleted ? (string?)null : item.Review.TargetType == ReviewTargetType.Blogger ? item.Review.Deal.Business.LogoUrl : item.Review.Deal.Blogger.AvatarUrl,
                item.AuthorDeleted));
}
