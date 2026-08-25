using BloggerBazar.Application.Features.Campaigns;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignManagementReadModel
{
    Task<MyCampaignsResult> SearchAsync(Guid businessId, MyCampaignsSearch search, CancellationToken cancellationToken);
    Task<MyCampaignDetailsDto?> GetByIdAsync(Guid businessId, Guid campaignId, CancellationToken cancellationToken);
}
