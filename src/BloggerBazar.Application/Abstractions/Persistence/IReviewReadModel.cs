using BloggerBazar.Application.Features.Reviews;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IReviewReadModel
{
    Task<IReadOnlyList<ReviewDto>> GetBloggerReviewsAsync(Guid bloggerId, int take, CancellationToken cancellationToken);
}
