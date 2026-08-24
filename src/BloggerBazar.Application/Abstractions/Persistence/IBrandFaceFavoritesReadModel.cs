using BloggerBazar.Application.Features.Favorites;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IBrandFaceFavoritesReadModel
{
    Task<BrandFaceFavoritesPageDto> GetBrandFaceFavoritesAsync(Guid platformUserId, int page, int pageSize, CancellationToken cancellationToken);
}
