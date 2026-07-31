using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class FavoriteRepository(BloggerBazarDbContext dbContext) : IFavoriteRepository
{
    public Task<Favorite?> GetAsync(Guid platformUserId, Guid bloggerId, CancellationToken cancellationToken) =>
        dbContext.Favorites.SingleOrDefaultAsync(favorite => favorite.PlatformUserId == platformUserId && favorite.BloggerId == bloggerId, cancellationToken);

    public async Task AddAsync(Favorite favorite, CancellationToken cancellationToken) =>
        await dbContext.Favorites.AddAsync(favorite, cancellationToken);

    public async Task<bool> DeleteAsync(Guid platformUserId, Guid bloggerId, CancellationToken cancellationToken) =>
        await dbContext.Favorites.Where(favorite => favorite.PlatformUserId == platformUserId && favorite.BloggerId == bloggerId)
            .ExecuteDeleteAsync(cancellationToken) == 1;

    public async Task DeleteForPlatformUserAsync(Guid platformUserId, CancellationToken cancellationToken) =>
        await dbContext.Favorites.Where(favorite => favorite.PlatformUserId == platformUserId).ExecuteDeleteAsync(cancellationToken);

    public async Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) =>
        await dbContext.Favorites.Where(favorite => favorite.BloggerId == bloggerId).ExecuteDeleteAsync(cancellationToken);
}
