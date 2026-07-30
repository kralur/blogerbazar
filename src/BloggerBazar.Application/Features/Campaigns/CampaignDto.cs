using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record CampaignDto(Guid Id, Guid BusinessId, string BusinessName, string Title, string Description, string? City, IReadOnlyCollection<string> Categories, IReadOnlyCollection<string> Requirements, int? BudgetFrom, int? BudgetTo, DateTime? Deadline, bool IsPromoted, int Status, int ApplicationsCount, DateTime CreatedAtUtc)
{
    public static CampaignDto From(Campaign campaign, string businessName) => new(
        campaign.Id, campaign.BusinessId, businessName, campaign.Title, campaign.Description, campaign.City,
        campaign.Categories, campaign.Requirements, campaign.BudgetFrom, campaign.BudgetTo, campaign.Deadline, campaign.IsPromoted, (int)campaign.Status, campaign.Applications.Count, campaign.CreatedAtUtc);
}
