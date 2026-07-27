using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class ReviewRepository(BloggerBazarDbContext dbContext) : IReviewRepository
{
    public Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken) =>
        dbContext.Reviews.AnyAsync(review => review.DealId == dealId && review.ReviewerTelegramUserId == reviewerTelegramUserId, cancellationToken);

    public async Task<IReadOnlyList<Review>> GetForBloggerAsync(Guid bloggerId, int take, CancellationToken cancellationToken) =>
        await dbContext.Reviews.AsNoTracking().Include(review => review.Deal).ThenInclude(deal => deal.Business).Where(review => review.BloggerId == bloggerId)
            .OrderByDescending(review => review.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task AddAsync(Review review, CancellationToken cancellationToken) => await dbContext.Reviews.AddAsync(review, cancellationToken);
}
