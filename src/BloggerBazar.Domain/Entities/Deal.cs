using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class Deal
{
    private Deal() { }

    private Deal(Guid? campaignApplicationId, Guid? collaborationRequestId, MarketplaceRole creatorRole, Guid creatorId, Guid businessId, CampaignTermsSnapshot? campaignTermsSnapshot = null)
    {
        Id = Guid.NewGuid();
        CampaignApplicationId = campaignApplicationId;
        CollaborationRequestId = collaborationRequestId;
        if (creatorRole == MarketplaceRole.BrandFace) BrandFaceId = creatorId;
        else if (creatorRole == MarketplaceRole.Blogger) BloggerId = creatorId;
        else throw new ArgumentOutOfRangeException(nameof(creatorRole), "A deal creator is a blogger or a brand face.");
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
    // Exactly one creator side: a blogger or a brand face (D46).
    public Guid? BloggerId { get; private set; }
    public BloggerProfile? Blogger { get; private set; }
    public Guid? BrandFaceId { get; private set; }
    public BrandFaceProfile? BrandFace { get; private set; }
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
    // The price the business entered for a deal without an offer budget (D51); an offer's budget is already agreed.
    public int? AgreedPrice { get; private set; }
    public DateTime? AgreedPriceSetAtUtc { get; private set; }
    public IReadOnlyCollection<DealPublication> Publications { get; private set; } = new List<DealPublication>();

    public MarketplaceRole CreatorRole => BrandFaceId.HasValue ? MarketplaceRole.BrandFace : MarketplaceRole.Blogger;
    public Guid CreatorId => BloggerId ?? BrandFaceId ?? Guid.Empty;
    public string CreatorName => Blogger?.Name ?? BrandFace?.Name ?? string.Empty;
    public long CreatorTelegramUserId => Blogger?.TelegramUserId ?? BrandFace?.TelegramUserId ?? 0;

    public static Deal Create(Guid campaignApplicationId, Guid bloggerId, Guid businessId) => new(campaignApplicationId, null, MarketplaceRole.Blogger, bloggerId, businessId);

    public static Deal Create(Guid campaignApplicationId, Guid bloggerId, Guid businessId, CampaignTermsSnapshot campaignTermsSnapshot) =>
        Create(campaignApplicationId, MarketplaceRole.Blogger, bloggerId, businessId, campaignTermsSnapshot);

    public static Deal Create(Guid campaignApplicationId, MarketplaceRole creatorRole, Guid creatorId, Guid businessId, CampaignTermsSnapshot campaignTermsSnapshot) =>
        new(campaignApplicationId, null, creatorRole, creatorId, businessId, campaignTermsSnapshot ?? throw new ArgumentNullException(nameof(campaignTermsSnapshot)));

    public static Deal CreateFromCollaborationRequest(Guid collaborationRequestId, Guid bloggerId, Guid businessId) =>
        new(null, collaborationRequestId, MarketplaceRole.Blogger, bloggerId, businessId);

    public static Deal CreateFromCollaborationRequest(Guid collaborationRequestId, MarketplaceRole creatorRole, Guid creatorId, Guid businessId) =>
        new(null, collaborationRequestId, creatorRole, creatorId, businessId);

    public void SetAgreedPrice(int price, DateTime utcNow)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "A price cannot be negative.");
        AgreedPrice = price;
        AgreedPriceSetAtUtc = utcNow;
    }

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
