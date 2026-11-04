using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICollaborationRequestRepository
{
    Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken);
    Task AddAsync(CollaborationRequest request, CancellationToken cancellationToken);
    Task<CollaborationRequest?> GetOfferForParticipantAsync(Guid offerId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult<CollaborationRequest?>(null);
    Task<IReadOnlyList<CollaborationRequest>> ListOffersForParticipantAsync(MarketplaceRole role, Guid profileId, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CollaborationRequest>>([]);
    Task<CollaborationRequest?> GetPendingOfferAsync(Guid businessId, MarketplaceRole creatorRole, Guid creatorId, CancellationToken cancellationToken) =>
        Task.FromResult<CollaborationRequest?>(null);
    Task<int> CountOffersSinceAsync(Guid businessId, DateTime sinceUtc, CancellationToken cancellationToken) => Task.FromResult(0);
    // Pre-launch audit 2026-11-04: answers an offer only while it is still pending (conditional update), so Accept and
    // Decline tapped at once cannot both win; false when the other answer came first.
    Task<bool> TryDecidePendingAsync(Guid offerId, CollaborationRequestStatus status, CancellationToken cancellationToken) => Task.FromResult(true);
}
