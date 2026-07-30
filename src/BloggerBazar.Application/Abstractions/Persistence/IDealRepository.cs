using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IDealRepository
{
    Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken);
    Task AddAsync(Deal deal, CancellationToken cancellationToken);
}
