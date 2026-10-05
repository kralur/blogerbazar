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
    bool BusinessHasReviewed);

internal sealed record DealView(
    string SourceType,
    string TermsSource,
    DealTermsDto? Terms,
    string CounterpartyName,
    string? CounterpartyImageUrl,
    bool CanComplete,
    bool CanReview,
    bool HasReviewed)
{
    public static DealView From(DealReadRow row, MarketplaceRole viewerRole)
    {
        var viewerIsBlogger = viewerRole == MarketplaceRole.Blogger;
        var hasReviewed = viewerIsBlogger ? row.BloggerHasReviewed : row.BusinessHasReviewed;
        var (sourceType, termsSource, terms) = ResolveTerms(row);

        return new(
            sourceType,
            termsSource,
            terms,
            viewerIsBlogger ? row.BusinessName : row.BloggerName,
            viewerIsBlogger ? row.BusinessLogoUrl : row.BloggerAvatarUrl,
            row.Status == DealStatus.Active,
            row.Status == DealStatus.Completed && !hasReviewed,
            hasReviewed);
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
