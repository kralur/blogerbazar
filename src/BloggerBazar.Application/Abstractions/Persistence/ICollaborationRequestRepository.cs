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
    Task<CollaborationRequest?> GetPendingOfferAsync(Guid businessId, Guid bloggerId, CancellationToken cancellationToken) =>
        Task.FromResult<CollaborationRequest?>(null);
    Task<int> CountOffersSinceAsync(Guid businessId, DateTime sinceUtc, CancellationToken cancellationToken) => Task.FromResult(0);
}
