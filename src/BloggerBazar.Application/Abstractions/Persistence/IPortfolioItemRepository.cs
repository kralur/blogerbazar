using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IPortfolioItemRepository
{
    Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<PortfolioItem> portfolioItems, CancellationToken cancellationToken);
}
