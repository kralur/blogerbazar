using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IFavoriteRepository
{
    Task<Favorite?> GetAsync(Guid platformUserId, Guid bloggerId, CancellationToken cancellationToken);
    Task AddAsync(Favorite favorite, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid platformUserId, Guid bloggerId, CancellationToken cancellationToken);
    Task DeleteForPlatformUserAsync(Guid platformUserId, CancellationToken cancellationToken);
    Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken);
}
