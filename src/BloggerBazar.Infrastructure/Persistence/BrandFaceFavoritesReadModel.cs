using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Favorites;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BrandFaceFavoritesReadModel(BloggerBazarDbContext dbContext) : IBrandFaceFavoritesReadModel
{
    public async Task<BrandFaceFavoritesPageDto> GetBrandFaceFavoritesAsync(Guid platformUserId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var publicBrandFaces = BrandFaceCatalogVisibility.PublicBrandFaces(
            dbContext.BrandFaceProfiles.AsNoTracking(),
            dbContext.PlatformUsers.AsNoTracking());
        var query = from favorite in dbContext.BrandFaceFavorites.AsNoTracking()
                    join brandFace in publicBrandFaces on favorite.BrandFaceId equals brandFace.Id
                    where favorite.PlatformUserId == platformUserId
                    select new { Favorite = favorite, BrandFace = brandFace };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.Favorite.CreatedAtUtc)
            .ThenByDescending(item => item.Favorite.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new BrandFaceFavoriteDto(
                item.BrandFace.Id,
                item.BrandFace.Name,
                item.BrandFace.City,
                item.BrandFace.Categories,
                item.BrandFace.Languages,
                item.BrandFace.CollaborationPrice,
                item.BrandFace.AvatarUrl,
                item.BrandFace.IsPromoted))
            .ToArrayAsync(cancellationToken);

        return new BrandFaceFavoritesPageDto(items, total, page, pageSize, total > (long)page * pageSize);
    }
}
