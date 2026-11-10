using BloggerBazar.Domain.Enums;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IReviewRepository
{
    // Sees hidden reviews too: a reviewer's own unpublished review still counts as written.
    // D64 stage C: one review per side of the deal (the company's, whoever in its team writes it, and the creator's).
    Task<bool> ExistsAsync(Guid dealId, ReviewTargetType targetType, CancellationToken cancellationToken);
    Task AddAsync(Review review, CancellationToken cancellationToken);

    // Publishes hidden reviews whose partner review exists or whose review window has ended; dealId null means all deals.
    Task<int> PublishRevealedAsync(Guid? dealId, DateTime nowUtc, CancellationToken cancellationToken);
}
