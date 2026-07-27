using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Api.Contracts.Admin;

public sealed record ModerateCampaignRequest(CampaignStatus? Status, bool? IsPromoted);
