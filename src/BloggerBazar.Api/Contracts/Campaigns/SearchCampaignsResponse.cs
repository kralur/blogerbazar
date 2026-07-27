using BloggerBazar.Application.Features.Campaigns;

namespace BloggerBazar.Api.Contracts.Campaigns;

public sealed record SearchCampaignsResponse(IReadOnlyList<CampaignDto> Campaigns);
