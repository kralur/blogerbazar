using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record CampaignDto(Guid Id, Guid BusinessId, string BusinessName, string Title, string Description, string? City, IReadOnlyCollection<string> Categories, int? BudgetFrom, int? BudgetTo, bool IsPromoted, int Status, DateTime CreatedAtUtc)
{
    public static CampaignDto From(Campaign campaign, string businessName) => new(
        campaign.Id, campaign.BusinessId, businessName, campaign.Title, campaign.Description, campaign.City,
        campaign.Categories, campaign.BudgetFrom, campaign.BudgetTo, campaign.IsPromoted, (int)campaign.Status, campaign.CreatedAtUtc);
}
