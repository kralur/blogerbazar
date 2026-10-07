using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Notifications;

// What a bot message calls a deal: the campaign title, or the offer format for a deal without a campaign.
internal sealed record DealTopic(string? CampaignTitle, CollaborationFormat? Format)
{
    public static DealTopic Of(Deal deal) => new(deal.CampaignTitleSnapshot, deal.CollaborationRequest?.Format);
}
