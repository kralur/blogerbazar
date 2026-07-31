using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Favorites;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class FavoritesReadModel(BloggerBazarDbContext dbContext) : IFavoritesReadModel
{
    public async Task<FavoritesPageDto> GetFavoritesAsync(Guid platformUserId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = from favorite in dbContext.Favorites.AsNoTracking()
                    join blogger in dbContext.BloggerProfiles.AsNoTracking() on favorite.BloggerId equals blogger.Id
                    where favorite.PlatformUserId == platformUserId
                        && !blogger.IsDeleted
                        && blogger.Status == BloggerBazar.Domain.Enums.BloggerStatus.Approved
                    select new { Favorite = favorite, Blogger = blogger };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.Favorite.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new FavoriteBloggerDto(
                item.Favorite.BloggerId,
                item.Blogger.Name,
                item.Blogger.City,
                item.Blogger.Categories,
                item.Blogger.AvatarUrl,
                item.Blogger.TotalFollowers,
                item.Favorite.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new FavoritesPageDto(items, total, page, pageSize);
    }
}
