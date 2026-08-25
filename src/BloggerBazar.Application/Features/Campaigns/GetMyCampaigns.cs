using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record SearchMyCampaignsQuery(long TelegramUserId, int? Status, string? Query, string? Sort, int Page, int PageSize)
    : IRequest<MyCampaignsResult>;

public sealed record GetMyCampaignQuery(long TelegramUserId, Guid CampaignId) : IRequest<MyCampaignDetailsDto?>;

public sealed record MyCampaignsSearch(int? Status, string? Query, string Sort, int Page, int PageSize);

public sealed record MyCampaignsResult(IReadOnlyList<MyCampaignItemDto> Items, int Total, int Page, int PageSize, bool HasMore);

public sealed record MyCampaignItemDto(
    Guid Id,
    string Title,
    string? City,
    IReadOnlyCollection<string> Categories,
    int? MinBudget,
    int? MaxBudget,
    DateTime? Deadline,
    int Status,
    bool IsPromoted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int ApplicationsCount);

public sealed record MyCampaignDetailsDto(
    Guid Id,
    string Title,
    string Description,
    string? City,
    IReadOnlyCollection<string> Categories,
    IReadOnlyCollection<string> Requirements,
    int? MinBudget,
    int? MaxBudget,
    DateTime? Deadline,
    int Status,
    bool IsPromoted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int ApplicationsCount);

public sealed class SearchMyCampaignsValidator : AbstractValidator<SearchMyCampaignsQuery>
{
    private static readonly string[] AllowedSorts = ["newest", "oldest", "deadline_asc", "deadline_desc", "budget_asc", "budget_desc"];

    public SearchMyCampaignsValidator()
    {
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
        RuleFor(query => query.Status)
            .Must(status => !status.HasValue || Enum.IsDefined((CampaignStatus)status.Value))
            .WithMessage("The campaign status is invalid.");
        RuleFor(query => query.Query).MaximumLength(100).When(query => query.Query is not null);
        RuleFor(query => query.Sort)
            .NotEmpty()
            .Must(sort => AllowedSorts.Contains(sort, StringComparer.Ordinal))
            .WithMessage("The campaign sort is invalid.");
        RuleFor(query => query.Page).InclusiveBetween(1, 100_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class GetMyCampaignValidator : AbstractValidator<GetMyCampaignQuery>
{
    public GetMyCampaignValidator()
    {
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
        RuleFor(query => query.CampaignId).NotEmpty();
    }
}

public sealed class SearchMyCampaignsHandler(
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    ICampaignManagementReadModel campaigns) : IRequestHandler<SearchMyCampaignsQuery, MyCampaignsResult>
{
    public async Task<MyCampaignsResult> Handle(SearchMyCampaignsQuery query, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, query.TelegramUserId, cancellationToken);
        var search = new MyCampaignsSearch(
            query.Status,
            string.IsNullOrWhiteSpace(query.Query) ? null : query.Query.Trim(),
            query.Sort ?? "newest",
            query.Page,
            query.PageSize);
        return await campaigns.SearchAsync(business.Id, search, cancellationToken);
    }
}

public sealed class GetMyCampaignHandler(
    IPlatformUserRepository users,
    IBusinessProfileRepository businesses,
    ICampaignManagementReadModel campaigns) : IRequestHandler<GetMyCampaignQuery, MyCampaignDetailsDto?>
{
    public async Task<MyCampaignDetailsDto?> Handle(GetMyCampaignQuery query, CancellationToken cancellationToken)
    {
        var business = await CampaignManagementAccess.RequireBusinessAsync(users, businesses, query.TelegramUserId, cancellationToken);
        return await campaigns.GetByIdAsync(business.Id, query.CampaignId, cancellationToken);
    }
}

internal static class CampaignManagementAccess
{
    internal static async Task<BusinessProfile> RequireBusinessAsync(
        IPlatformUserRepository users,
        IBusinessProfileRepository businesses,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole != MarketplaceRole.Business)
        {
            throw new UnauthorizedAccessException("An active business marketplace role is required.");
        }

        return await businesses.GetByTelegramUserIdAsync(telegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("An active business profile is required.");
    }
}
