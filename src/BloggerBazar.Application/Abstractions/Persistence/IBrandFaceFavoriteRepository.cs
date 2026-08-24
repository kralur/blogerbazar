using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IBrandFaceFavoriteRepository
{
    Task<BrandFaceFavorite?> GetAsync(Guid platformUserId, Guid brandFaceId, CancellationToken cancellationToken);
    Task AddAsync(BrandFaceFavorite favorite, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid platformUserId, Guid brandFaceId, CancellationToken cancellationToken);
    Task DeleteForPlatformUserAsync(Guid platformUserId, CancellationToken cancellationToken);
    Task DeleteForBrandFaceAsync(Guid brandFaceId, CancellationToken cancellationToken);
}
