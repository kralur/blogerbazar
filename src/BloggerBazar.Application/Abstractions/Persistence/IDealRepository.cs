using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IDealRepository
{
    Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Deal?> GetByCampaignApplicationIdAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult<Deal?>(null);
    Task<Deal?> GetByCollaborationRequestIdAsync(Guid collaborationRequestId, CancellationToken cancellationToken) => Task.FromResult<Deal?>(null);
    Task<Deal?> GetForParticipantAsync(Guid dealId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken) => Task.FromResult<Deal?>(null);
    Task<bool> TryCompleteAsync(Guid dealId, DateTime completedAtUtc, CancellationToken cancellationToken) => Task.FromResult(false);
    Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken);
    Task AddAsync(Deal deal, CancellationToken cancellationToken);
}
