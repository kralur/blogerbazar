using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class PortfolioItemRepository(BloggerBazarDbContext dbContext) : IPortfolioItemRepository
{
    public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) =>
        dbContext.PortfolioItems.Where(item => item.BloggerId == bloggerId).ExecuteDeleteAsync(cancellationToken);

    public async Task AddRangeAsync(IEnumerable<PortfolioItem> portfolioItems, CancellationToken cancellationToken) =>
        await dbContext.PortfolioItems.AddRangeAsync(portfolioItems, cancellationToken);
}
