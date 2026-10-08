using BloggerBazar.Application.Features.Reviews;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IReviewReadModel
{
    Task<IReadOnlyList<ReviewDto>> GetBloggerReviewsAsync(Guid bloggerId, int skip, int take, CancellationToken cancellationToken);
    Task<BusinessReviewsDto> GetBusinessReviewsAsync(Guid businessId, int skip, int take, CancellationToken cancellationToken);
}
