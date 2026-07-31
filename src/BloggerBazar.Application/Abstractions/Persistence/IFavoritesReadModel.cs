using BloggerBazar.Application.Features.Favorites;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IFavoritesReadModel
{
    Task<FavoritesPageDto> GetFavoritesAsync(Guid platformUserId, int page, int pageSize, CancellationToken cancellationToken);
}
