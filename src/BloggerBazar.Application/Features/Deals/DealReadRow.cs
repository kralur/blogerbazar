using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Features.Deals;

public static class DealSourceTypes
{
    public const string CampaignApplication = "campaignApplication";
    public const string CollaborationRequest = "collaborationRequest";
}

public static class DealTermsSources
{
    public const string Snapshot = "snapshot";
    public const string LiveCampaignFallback = "liveCampaignFallback";
    public const string Collaboration = "collaboration";
}

public sealed record DealTermsDto(
    string Title,
    string Description,
    string? City,
    IReadOnlyCollection<string> Categories,
    IReadOnlyCollection<string> Requirements,
    int? BudgetFrom,
    int? BudgetTo,
    DateTime? Deadline);

public sealed record DealReadRow(
    Guid Id,
    Guid? CampaignApplicationId,
    Guid? CollaborationRequestId,
    DealStatus Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string BloggerName,
    string? BloggerAvatarUrl,
    string BusinessName,
    string? BusinessLogoUrl,
    short? SnapshotVersion,
    string? SnapshotTitle,
    string? SnapshotDescription,
    string? SnapshotCity,
    IReadOnlyCollection<string>? SnapshotCategories,
    IReadOnlyCollection<string>? SnapshotRequirements,
    int? SnapshotBudgetFrom,
    int? SnapshotBudgetTo,
    DateTime? SnapshotDeadline,
    string? LiveCampaignTitle,
    string? LiveCampaignDescription,
    string? LiveCampaignCity,
    IReadOnlyCollection<string>? LiveCampaignCategories,
    IReadOnlyCollection<string>? LiveCampaignRequirements,
    int? LiveCampaignBudgetFrom,
    int? LiveCampaignBudgetTo,
    DateTime? LiveCampaignDeadline,
    bool BloggerHasReviewed,
    bool BusinessHasReviewed,
    CollaborationFormat? OfferFormat = null,
    int? OfferedBudget = null,
    DateTime? OfferDeadline = null,
    string? OfferMessage = null,
    Guid? BloggerId = null,
    Guid? BusinessId = null,
    bool BloggerDeleted = false,
    bool BusinessDeleted = false,
    Guid? BrandFaceId = null,
    int? AgreedPrice = null)
{
    // The "Blogger*" fields carry the creator side, a blogger or a brand face (D46).
    public MarketplaceRole CreatorRole => BrandFaceId.HasValue ? MarketplaceRole.BrandFace : MarketplaceRole.Blogger;
    public Guid? CreatorId => BloggerId ?? BrandFaceId;
}

public sealed record DealOfferDto(string? Format, int? OfferedBudget, DateTime? Deadline, string Message);

internal sealed record DealView(
    string SourceType,
    string TermsSource,
    DealTermsDto? Terms,
    string CounterpartyName,
    string? CounterpartyImageUrl,
    bool CanComplete,
    bool CanReview,
    bool HasReviewed,
    DealOfferDto? Offer,
    DateTime? ReviewDeadlineUtc,
    bool PartnerHasReviewed,
    bool CounterpartyDeleted,
    Guid? CounterpartyProfileId,
    string CounterpartyRole)
{
    public const string BusinessCounterparty = "business";

    public static DealView From(DealReadRow row, MarketplaceRole viewerRole) => From(row, viewerRole, DateTime.UtcNow);

    public static DealView From(DealReadRow row, MarketplaceRole viewerRole, DateTime nowUtc)
    {
        var viewerIsBlogger = viewerRole is MarketplaceRole.Blogger or MarketplaceRole.BrandFace;
        var hasReviewed = viewerIsBlogger ? row.BloggerHasReviewed : row.BusinessHasReviewed;
        // A deleted partner: the deal and its terms stay, the partner's identity and every action go.
        var partnerDeleted = viewerIsBlogger ? row.BusinessDeleted : row.BloggerDeleted;
        var (sourceType, termsSource, terms) = ResolveTerms(row);

        return new(
            sourceType,
            termsSource,
            terms,
            partnerDeleted ? string.Empty : viewerIsBlogger ? row.BusinessName : row.BloggerName,
            partnerDeleted ? (string?)null : viewerIsBlogger ? row.BusinessLogoUrl : row.BloggerAvatarUrl,
            !partnerDeleted && row.Status == DealStatus.Active,
            !partnerDeleted && row.Status == DealStatus.Completed && !hasReviewed && Reviews.ReviewWindow.IsOpen(row.CompletedAtUtc, nowUtc),
            hasReviewed,
            row.CollaborationRequestId is not null && row.OfferMessage is not null
                ? new DealOfferDto(Offers.OfferFormats.ToName(row.OfferFormat), row.OfferedBudget, row.OfferDeadline, row.OfferMessage)
                : null,
            row.Status == DealStatus.Completed ? Reviews.ReviewWindow.EndsAtUtc(row.CompletedAtUtc) : null,
            // Only that a review exists, never its rating: the blind rule still hides the content.
            viewerIsBlogger ? row.BusinessHasReviewed : row.BloggerHasReviewed,
            partnerDeleted,
            partnerDeleted ? null : viewerIsBlogger ? row.BusinessId : row.CreatorId,
            viewerIsBlogger ? BusinessCounterparty : Campaigns.CreatorRoles.Of(row.CreatorRole));
    }

    private static (string SourceType, string TermsSource, DealTermsDto? Terms) ResolveTerms(DealReadRow row)
    {
        if (row.CampaignApplicationId is null)
        {
            return (DealSourceTypes.CollaborationRequest, DealTermsSources.Collaboration, null);
        }

        if (row.SnapshotVersion is not null)
        {
            return (DealSourceTypes.CampaignApplication, DealTermsSources.Snapshot, new DealTermsDto(
                row.SnapshotTitle ?? string.Empty,
                row.SnapshotDescription ?? string.Empty,
                row.SnapshotCity,
                row.SnapshotCategories ?? [],
                row.SnapshotRequirements ?? [],
                row.SnapshotBudgetFrom,
                row.SnapshotBudgetTo,
                row.SnapshotDeadline));
        }

        var liveTerms = row.LiveCampaignTitle is null
            ? null
            : new DealTermsDto(
                row.LiveCampaignTitle,
                row.LiveCampaignDescription ?? string.Empty,
                row.LiveCampaignCity,
                row.LiveCampaignCategories ?? [],
                row.LiveCampaignRequirements ?? [],
                row.LiveCampaignBudgetFrom,
                row.LiveCampaignBudgetTo,
                row.LiveCampaignDeadline);
        return (DealSourceTypes.CampaignApplication, DealTermsSources.LiveCampaignFallback, liveTerms);
    }
}
