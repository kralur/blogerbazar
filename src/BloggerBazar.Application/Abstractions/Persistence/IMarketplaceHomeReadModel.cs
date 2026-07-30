using BloggerBazar.Application.Features.Marketplace;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IMarketplaceHomeReadModel
{
    Task<MarketplaceHomeDto> GetAsync(CancellationToken cancellationToken);
}
