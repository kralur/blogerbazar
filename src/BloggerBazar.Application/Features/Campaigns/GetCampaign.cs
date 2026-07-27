using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record GetCampaignQuery(Guid Id) : IRequest<CampaignDto?>;

public sealed class GetCampaignHandler(ICampaignRepository campaigns) : IRequestHandler<GetCampaignQuery, CampaignDto?>
{
    public async Task<CampaignDto?> Handle(GetCampaignQuery query, CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(query.Id, cancellationToken);
        return campaign is null || campaign.Status != CampaignStatus.Published
            ? null
            : CampaignDto.From(campaign, campaign.Business.Name);
    }
}
