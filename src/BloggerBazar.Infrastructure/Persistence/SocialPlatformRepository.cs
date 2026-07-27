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
}
