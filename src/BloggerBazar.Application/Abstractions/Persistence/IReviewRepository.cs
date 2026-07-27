using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IReviewRepository
{
    Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Review>> GetForBloggerAsync(Guid bloggerId, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Review>>([]);
    Task AddAsync(Review review, CancellationToken cancellationToken);
}
