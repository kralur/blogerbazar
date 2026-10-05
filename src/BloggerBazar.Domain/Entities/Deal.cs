using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class Deal
{
    private Deal() { }

    private Deal(Guid? campaignApplicationId, Guid? collaborationRequestId, Guid bloggerId, Guid businessId, CampaignTermsSnapshot? campaignTermsSnapshot = null)
    {
        Id = Guid.NewGuid();
        CampaignApplicationId = campaignApplicationId;
        CollaborationRequestId = collaborationRequestId;
        BloggerId = bloggerId;
        BusinessId = businessId;
        Status = DealStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;

        if (campaignTermsSnapshot is not null)
        {
            CampaignTermsSnapshotVersion = CampaignTermsSnapshot.Version;
            CampaignTitleSnapshot = campaignTermsSnapshot.Title;
            CampaignDescriptionSnapshot = campaignTermsSnapshot.Description;
            CampaignCitySnapshot = campaignTermsSnapshot.City;
            CampaignCategoriesSnapshot = Array.AsReadOnly(campaignTermsSnapshot.Categories.ToArray());
            CampaignRequirementsSnapshot = Array.AsReadOnly(campaignTermsSnapshot.Requirements.ToArray());
            CampaignBudgetFromSnapshot = campaignTermsSnapshot.BudgetFrom;
            CampaignBudgetToSnapshot = campaignTermsSnapshot.BudgetTo;
            CampaignDeadlineSnapshot = campaignTermsSnapshot.Deadline;
        }
    }

    public Guid Id { get; private set; }
    public Guid? CampaignApplicationId { get; private set; }
    public CampaignApplication? CampaignApplication { get; private set; }
    public Guid? CollaborationRequestId { get; private set; }
    public CollaborationRequest? CollaborationRequest { get; private set; }
    public Guid BloggerId { get; private set; }
    public BloggerProfile Blogger { get; private set; } = null!;
    public Guid BusinessId { get; private set; }
    public BusinessProfile Business { get; private set; } = null!;
    public DealStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public short? CampaignTermsSnapshotVersion { get; private set; }
    public string? CampaignTitleSnapshot { get; private set; }
    public string? CampaignDescriptionSnapshot { get; private set; }
    public string? CampaignCitySnapshot { get; private set; }
    public IReadOnlyCollection<string>? CampaignCategoriesSnapshot { get; private set; }
    public IReadOnlyCollection<string>? CampaignRequirementsSnapshot { get; private set; }
    public int? CampaignBudgetFromSnapshot { get; private set; }
    public int? CampaignBudgetToSnapshot { get; private set; }
    public DateTime? CampaignDeadlineSnapshot { get; private set; }
    public IReadOnlyCollection<Review> Reviews { get; private set; } = new List<Review>();

    public static Deal Create(Guid campaignApplicationId, Guid bloggerId, Guid businessId) => new(campaignApplicationId, null, bloggerId, businessId);

    public static Deal Create(Guid campaignApplicationId, Guid bloggerId, Guid businessId, CampaignTermsSnapshot campaignTermsSnapshot) =>
        new(campaignApplicationId, null, bloggerId, businessId, campaignTermsSnapshot ?? throw new ArgumentNullException(nameof(campaignTermsSnapshot)));

    public static Deal CreateFromCollaborationRequest(Guid collaborationRequestId, Guid bloggerId, Guid businessId) =>
        new(null, collaborationRequestId, bloggerId, businessId);

    public void Complete()
    {
        if (Status != DealStatus.Active)
        {
            throw new InvalidOperationException("Only active deals can be completed.");
        }

        Status = DealStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
