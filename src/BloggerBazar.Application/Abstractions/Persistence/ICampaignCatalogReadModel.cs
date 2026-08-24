using BloggerBazar.Application.Features.Campaigns;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignCatalogReadModel
{
    Task<CampaignCatalogResult> SearchAsync(CampaignCatalogSearch search, CancellationToken cancellationToken);
}
