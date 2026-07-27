using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Features.Deals;

public sealed record DealDto(Guid Id, Guid? CampaignApplicationId, Guid? CollaborationRequestId, Guid BloggerId, Guid BusinessId, int Status, DateTime CreatedAtUtc, DateTime? CompletedAtUtc)
{
    public static DealDto From(Deal deal) => new(deal.Id, deal.CampaignApplicationId, deal.CollaborationRequestId, deal.BloggerId, deal.BusinessId, (int)deal.Status, deal.CreatedAtUtc, deal.CompletedAtUtc);
}
