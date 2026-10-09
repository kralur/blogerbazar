using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignApplicationRepository
{
    Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid campaignId, MarketplaceRole creatorRole, Guid creatorId, CancellationToken cancellationToken);
    Task AddAsync(CampaignApplication application, CancellationToken cancellationToken);
}
