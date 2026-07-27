using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IDealRepository
{
    Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Deal>> GetForParticipantsAsync(Guid? bloggerId, Guid? businessId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Deal>>([]);
    Task AddAsync(Deal deal, CancellationToken cancellationToken);
}
