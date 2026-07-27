using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Api.Contracts.Campaigns;

public sealed record UpdateCampaignApplicationStatusRequest(CampaignApplicationStatus Status);
