using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignApplicationReadModel
{
    Task<MyCampaignApplicationsResult> SearchForCreatorAsync(MarketplaceRole creatorRole, Guid creatorId, CampaignApplicationSearch search, CancellationToken cancellationToken);
    Task<MyCampaignApplicationDetailsDto?> GetForCreatorAsync(MarketplaceRole creatorRole, Guid creatorId, Guid applicationId, CancellationToken cancellationToken);
    Task<CampaignApplicationInboxResult> SearchForBusinessAsync(Guid businessId, Guid campaignId, CampaignApplicationSearch search, CancellationToken cancellationToken);
}
