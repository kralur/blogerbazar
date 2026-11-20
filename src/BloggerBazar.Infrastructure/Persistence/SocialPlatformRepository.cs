using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class SocialPlatformRepository(BloggerBazarDbContext dbContext) : ISocialPlatformRepository
{
    public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) =>
        dbContext.SocialPlatforms.Where(platform => platform.BloggerId == bloggerId).ExecuteDeleteAsync(cancellationToken);

    public async Task AddRangeAsync(IEnumerable<SocialPlatform> platforms, CancellationToken cancellationToken) =>
        await dbContext.SocialPlatforms.AddRangeAsync(platforms, cancellationToken);

    public async Task<IReadOnlyList<SocialPlatform>> ListForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) =>
        await dbContext.SocialPlatforms.AsNoTracking().Where(platform => platform.BloggerId == bloggerId).ToListAsync(cancellationToken);

    public Task<SocialPlatform?> GetForBloggerAsync(Guid bloggerId, Guid platformId, CancellationToken cancellationToken) =>
        dbContext.SocialPlatforms.SingleOrDefaultAsync(platform => platform.BloggerId == bloggerId && platform.Id == platformId, cancellationToken);

    public async Task<IReadOnlyList<SocialPlatform>> ListVerifiedBeforeAsync(DateTime beforeUtc, int take, CancellationToken cancellationToken) =>
        await dbContext.SocialPlatforms
            .Include(platform => platform.Blogger)
            .Where(platform => platform.VerifiedAtUtc != null && platform.VerifiedAtUtc < beforeUtc && !platform.Blogger.IsDeleted)
            .OrderBy(platform => platform.VerifiedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
}
