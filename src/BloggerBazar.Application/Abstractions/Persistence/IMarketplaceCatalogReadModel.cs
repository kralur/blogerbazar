using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Application.Features.Deals;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IMarketplaceCatalogReadModel
{
    Task<SearchBloggersResult> SearchBloggersAsync(BloggerCatalogSearch search, CancellationToken cancellationToken);
    Task<BloggerProfileDto?> GetBloggerAsync(Guid id, CancellationToken cancellationToken);
    Task<MyBloggerProfileDto?> GetMyBloggerAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CampaignDto>> SearchCampaignsAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken);
    Task<CampaignDto?> GetCampaignAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MyCampaignApplicationDto>> GetCampaignApplicationsAsync(Guid? bloggerId, Guid? businessId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequestsAsync(Guid? bloggerId, Guid? businessId, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<MyDealDto>> GetDealsAsync(Guid? bloggerId, Guid? businessId, long telegramUserId, CancellationToken cancellationToken);
}
