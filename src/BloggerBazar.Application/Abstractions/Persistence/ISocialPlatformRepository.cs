using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ISocialPlatformRepository
{
    Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<SocialPlatform> platforms, CancellationToken cancellationToken);
}
