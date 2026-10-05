namespace BloggerBazar.Domain.Entities;

public sealed class CampaignTermsSnapshot
{
    private CampaignTermsSnapshot(
        string title,
        string description,
        string? city,
        IReadOnlyCollection<string> categories,
        IReadOnlyCollection<string> requirements,
        int? budgetFrom,
        int? budgetTo,
        DateTime? deadline)
    {
        Title = title;
        Description = description;
        City = city;
        Categories = Array.AsReadOnly(categories.ToArray());
        Requirements = Array.AsReadOnly(requirements.ToArray());
        BudgetFrom = budgetFrom;
        BudgetTo = budgetTo;
        Deadline = deadline;
    }

    public const short Version = 1;

    public string Title { get; }
    public string Description { get; }
    public string? City { get; }
    public IReadOnlyCollection<string> Categories { get; }
    public IReadOnlyCollection<string> Requirements { get; }
    public int? BudgetFrom { get; }
    public int? BudgetTo { get; }
    public DateTime? Deadline { get; }

    public static CampaignTermsSnapshot FromCampaign(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        Validate(campaign.Title, campaign.Description, campaign.City, campaign.Categories, campaign.Requirements, campaign.BudgetFrom, campaign.BudgetTo);
        return new CampaignTermsSnapshot(
            campaign.Title,
            campaign.Description,
            campaign.City,
            campaign.Categories,
            campaign.Requirements,
            campaign.BudgetFrom,
            campaign.BudgetTo,
            campaign.Deadline);
    }

    private static void Validate(
        string title,
        string description,
        string? city,
        IReadOnlyCollection<string> categories,
        IReadOnlyCollection<string> requirements,
        int? budgetFrom,
        int? budgetTo)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160)
        {
            throw new ArgumentException("Campaign snapshot title is invalid.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > 3000)
        {
            throw new ArgumentException("Campaign snapshot description is invalid.", nameof(description));
        }

        if (city?.Length > 80)
        {
            throw new ArgumentException("Campaign snapshot city is invalid.", nameof(city));
        }

        if (categories.Count is 0 or > 5 || categories.Any(category => string.IsNullOrWhiteSpace(category) || category.Length > 50))
        {
            throw new ArgumentException("Campaign snapshot categories are invalid.", nameof(categories));
        }

        if (requirements.Count > 10 || requirements.Any(requirement => string.IsNullOrWhiteSpace(requirement) || requirement.Length > 300))
        {
            throw new ArgumentException("Campaign snapshot requirements are invalid.", nameof(requirements));
        }

        if (budgetFrom < 0 || budgetTo < 0 || (budgetFrom.HasValue && budgetTo.HasValue && budgetTo < budgetFrom))
        {
            throw new ArgumentException("Campaign snapshot budget is invalid.");
        }
    }
}
