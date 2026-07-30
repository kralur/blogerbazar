using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record GetCampaignQuery(Guid Id) : IRequest<CampaignDto?>;

public sealed class GetCampaignHandler(IMarketplaceCatalogReadModel catalog) : IRequestHandler<GetCampaignQuery, CampaignDto?>
{
    public async Task<CampaignDto?> Handle(GetCampaignQuery query, CancellationToken cancellationToken)
    {
        return await catalog.GetCampaignAsync(query.Id, cancellationToken);
    }
}
