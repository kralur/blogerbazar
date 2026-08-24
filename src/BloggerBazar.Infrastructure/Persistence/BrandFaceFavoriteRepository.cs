using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BrandFaceFavoriteRepository(BloggerBazarDbContext dbContext) : IBrandFaceFavoriteRepository
{
    public Task<BrandFaceFavorite?> GetAsync(Guid platformUserId, Guid brandFaceId, CancellationToken cancellationToken) =>
        dbContext.BrandFaceFavorites.SingleOrDefaultAsync(favorite => favorite.PlatformUserId == platformUserId && favorite.BrandFaceId == brandFaceId, cancellationToken);

    public async Task AddAsync(BrandFaceFavorite favorite, CancellationToken cancellationToken) =>
        await dbContext.BrandFaceFavorites.AddAsync(favorite, cancellationToken);

    public async Task<bool> DeleteAsync(Guid platformUserId, Guid brandFaceId, CancellationToken cancellationToken) =>
        await dbContext.BrandFaceFavorites.Where(favorite => favorite.PlatformUserId == platformUserId && favorite.BrandFaceId == brandFaceId)
            .ExecuteDeleteAsync(cancellationToken) == 1;

    public async Task DeleteForPlatformUserAsync(Guid platformUserId, CancellationToken cancellationToken) =>
        await dbContext.BrandFaceFavorites.Where(favorite => favorite.PlatformUserId == platformUserId).ExecuteDeleteAsync(cancellationToken);

    public async Task DeleteForBrandFaceAsync(Guid brandFaceId, CancellationToken cancellationToken) =>
        await dbContext.BrandFaceFavorites.Where(favorite => favorite.BrandFaceId == brandFaceId).ExecuteDeleteAsync(cancellationToken);
}
