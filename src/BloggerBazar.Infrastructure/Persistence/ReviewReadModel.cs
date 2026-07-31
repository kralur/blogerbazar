using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class ReviewReadModel(BloggerBazarDbContext dbContext) : IReviewReadModel
{
    public async Task<IReadOnlyList<ReviewDto>> GetBloggerReviewsAsync(Guid bloggerId, int take, CancellationToken cancellationToken) =>
        await dbContext.Reviews.AsNoTracking().Where(review => review.BloggerId == bloggerId
            && review.Blogger != null && !review.Blogger.IsDeleted
            && review.Business != null && !review.Business.IsDeleted)
            .OrderByDescending(review => review.CreatedAtUtc).Take(take)
            .Select(review => new ReviewDto(review.Id, review.DealId, (int)review.TargetType, review.Rating, review.Comment,
                review.TargetType == ReviewTargetType.Blogger ? review.Deal.Business.Name : review.Deal.Blogger.Name, review.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
}
