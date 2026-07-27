using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignApplicationRepository
{
    Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid campaignId, Guid bloggerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CampaignApplication>> GetForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CampaignApplication>>([]);
    Task<IReadOnlyList<CampaignApplication>> GetForBusinessAsync(Guid businessId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CampaignApplication>>([]);
    Task AddAsync(CampaignApplication application, CancellationToken cancellationToken);
}
