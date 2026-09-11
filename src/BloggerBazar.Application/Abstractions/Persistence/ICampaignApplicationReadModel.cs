using BloggerBazar.Application.Features.Campaigns;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignApplicationReadModel
{
    Task<MyCampaignApplicationsResult> SearchForBloggerAsync(Guid bloggerId, CampaignApplicationSearch search, CancellationToken cancellationToken);
    Task<MyCampaignApplicationDetailsDto?> GetForBloggerAsync(Guid bloggerId, Guid applicationId, CancellationToken cancellationToken);
    Task<CampaignApplicationInboxResult> SearchForBusinessAsync(Guid businessId, Guid campaignId, CampaignApplicationSearch search, CancellationToken cancellationToken);
}
