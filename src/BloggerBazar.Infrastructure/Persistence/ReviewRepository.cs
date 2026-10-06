using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class ReviewRepository(BloggerBazarDbContext dbContext) : IReviewRepository
{
    public Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken) =>
        dbContext.Reviews.IgnoreQueryFilters().AnyAsync(review => review.DealId == dealId && review.ReviewerTelegramUserId == reviewerTelegramUserId, cancellationToken);

    public async Task AddAsync(Review review, CancellationToken cancellationToken) => await dbContext.Reviews.AddAsync(review, cancellationToken);

    public Task<int> PublishRevealedAsync(Guid? dealId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var windowClosedBefore = nowUtc - ReviewWindow.Length;
        // IgnoreQueryFilters applies to the whole query, so the partner subquery sees hidden reviews as well.
        return dbContext.Reviews.IgnoreQueryFilters()
            .Where(review => review.PublishedAtUtc == null && (dealId == null || review.DealId == dealId))
            .Where(review => review.Deal.CompletedAtUtc <= windowClosedBefore
                || dbContext.Reviews.Any(partner => partner.DealId == review.DealId && partner.Id != review.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(review => review.PublishedAtUtc, (DateTime?)nowUtc), cancellationToken);
    }
}
