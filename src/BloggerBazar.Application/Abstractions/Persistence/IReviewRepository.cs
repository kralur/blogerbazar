using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IReviewRepository
{
    Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken);
    Task AddAsync(Review review, CancellationToken cancellationToken);
}
