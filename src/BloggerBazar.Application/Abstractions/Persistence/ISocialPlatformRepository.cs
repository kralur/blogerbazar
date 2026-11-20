using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ISocialPlatformRepository
{
    Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<SocialPlatform> platforms, CancellationToken cancellationToken);

    // D68: the platforms before the profile form recreates them (their verification carries over), one platform of
    // a blogger (foreign = missing), and verified platforms whose numbers are older than a date (weekly refresh).
    Task<IReadOnlyList<SocialPlatform>> ListForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SocialPlatform>>([]);
    Task<SocialPlatform?> GetForBloggerAsync(Guid bloggerId, Guid platformId, CancellationToken cancellationToken) =>
        Task.FromResult<SocialPlatform?>(null);
    Task<IReadOnlyList<SocialPlatform>> ListVerifiedBeforeAsync(DateTime beforeUtc, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SocialPlatform>>([]);
}
