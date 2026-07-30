using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignApplicationRepository
{
    Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid campaignId, Guid bloggerId, CancellationToken cancellationToken);
    Task AddAsync(CampaignApplication application, CancellationToken cancellationToken);
}
