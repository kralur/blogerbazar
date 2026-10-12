using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignApplicationRepository
{
    Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid campaignId, MarketplaceRole creatorRole, Guid creatorId, CancellationToken cancellationToken);
    Task AddAsync(CampaignApplication application, CancellationToken cancellationToken);

    // Moves an application that is still pending (sent or viewed) to the decided status in one conditional update.
    // False when a concurrent accept, reject or withdrawal committed first: the earlier decision wins, never both.
    // In-memory fakes have no concurrency, so they succeed by default.
    Task<bool> TryDecidePendingAsync(Guid applicationId, CampaignApplicationStatus status, CancellationToken cancellationToken) => Task.FromResult(true);
}
