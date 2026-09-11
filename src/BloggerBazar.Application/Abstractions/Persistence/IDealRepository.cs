using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IDealRepository
{
    Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Deal?> GetByCampaignApplicationIdAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult<Deal?>(null);
    Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken);
    Task AddAsync(Deal deal, CancellationToken cancellationToken);
}
