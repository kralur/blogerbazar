using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class ReviewRepository(BloggerBazarDbContext dbContext) : IReviewRepository
{
    public Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken) =>
        dbContext.Reviews.AnyAsync(review => review.DealId == dealId && review.ReviewerTelegramUserId == reviewerTelegramUserId, cancellationToken);

    public async Task AddAsync(Review review, CancellationToken cancellationToken) => await dbContext.Reviews.AddAsync(review, cancellationToken);
}
