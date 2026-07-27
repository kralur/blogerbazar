using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class Campaign
{
    private Campaign() { }

    private Campaign(Guid businessId, string title, string description, IReadOnlyCollection<string> categories, int? budgetFrom, int? budgetTo, string? city)
    {
        Id = Guid.NewGuid();
        BusinessId = businessId;
        Title = title;
        Description = description;
        Categories = categories.ToList();
        BudgetFrom = budgetFrom;
        BudgetTo = budgetTo;
        City = city;
        Status = CampaignStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public BusinessProfile Business { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string? City { get; private set; }
    public IReadOnlyCollection<string> Categories { get; private set; } = [];
    public int? BudgetFrom { get; private set; }
    public int? BudgetTo { get; private set; }
    public bool IsPromoted { get; private set; }
    public CampaignStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<CampaignApplication> Applications { get; private set; } = new List<CampaignApplication>();

    public static Campaign Create(Guid businessId, string title, string description, IReadOnlyCollection<string> categories, int? budgetFrom, int? budgetTo, string? city) =>
        new(businessId, title, description, categories, budgetFrom, budgetTo, city);

    public void Publish()
    {
        Status = CampaignStatus.Published;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = CampaignStatus.Archived;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetStatus(CampaignStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetPromotion(bool isPromoted)
    {
        IsPromoted = isPromoted;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
