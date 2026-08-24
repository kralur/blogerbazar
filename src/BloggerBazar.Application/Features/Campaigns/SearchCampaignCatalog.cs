using System.Globalization;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record SearchCampaignCatalogQuery(
    string? Query,
    string? City,
    string? Category,
    int? MinBudget,
    int? MaxBudget,
    DateTime? DeadlineFrom,
    DateTime? DeadlineTo,
    string? Sort = "promoted",
    int Page = 1,
    int PageSize = 20) : IRequest<CampaignCatalogResult>;

public sealed record CampaignCatalogItemDto(
    Guid Id,
    string Title,
    string BusinessName,
    string? BusinessAvatarUrl,
    string? City,
    IReadOnlyCollection<string> Categories,
    IReadOnlyCollection<string> Requirements,
    int? MinBudget,
    int? MaxBudget,
    DateTime? Deadline,
    int Status,
    bool IsPromoted,
    DateTime CreatedAtUtc);

public sealed record CampaignCatalogResult(
    IReadOnlyList<CampaignCatalogItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record CampaignCatalogSearch(
    string? Query,
    string? City,
    string? Category,
    int? MinBudget,
    int? MaxBudget,
    DateTime? DeadlineFrom,
    DateTime? DeadlineTo,
    string Sort,
    int Page,
    int PageSize);

public sealed class SearchCampaignCatalogValidator : AbstractValidator<SearchCampaignCatalogQuery>
{
    private static readonly string[] Sorts = ["promoted", "newest", "deadline_asc", "budget_asc", "budget_desc"];

    public SearchCampaignCatalogValidator()
    {
        RuleFor(query => query.Page).InclusiveBetween(1, 100_000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
        RuleFor(query => query.Query).MaximumLength(100).When(query => query.Query is not null);
        RuleFor(query => query.City).MaximumLength(80).When(query => query.City is not null);
        RuleFor(query => query.Category).MaximumLength(50).When(query => query.Category is not null);
        RuleFor(query => query.MinBudget).GreaterThanOrEqualTo(0).When(query => query.MinBudget.HasValue);
        RuleFor(query => query.MaxBudget).GreaterThanOrEqualTo(0).When(query => query.MaxBudget.HasValue);
        RuleFor(query => query).Must(query => !query.MinBudget.HasValue || !query.MaxBudget.HasValue || query.MinBudget <= query.MaxBudget)
            .WithMessage("Minimum budget must not exceed maximum budget.");
        RuleFor(query => query).Must(query => !query.DeadlineFrom.HasValue || !query.DeadlineTo.HasValue || query.DeadlineFrom <= query.DeadlineTo)
            .WithMessage("Deadline start must not be after deadline end.");
        RuleFor(query => query.Sort)
            .Must(sort => !string.IsNullOrWhiteSpace(sort) && Sorts.Contains(sort.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sort must be one of: promoted, newest, deadline_asc, budget_asc, budget_desc.");
    }
}

public sealed class SearchCampaignCatalogHandler(ICampaignCatalogReadModel catalog, ICatalogCache cache)
    : IRequestHandler<SearchCampaignCatalogQuery, CampaignCatalogResult>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    public async Task<CampaignCatalogResult> Handle(SearchCampaignCatalogQuery query, CancellationToken cancellationToken)
    {
        var search = Normalize(query);
        var namespaceVersion = await cache.GetNamespaceVersionAsync(CampaignCatalogCache.Namespace, cancellationToken);
        var key = $"catalog:{namespaceVersion}:campaigns:v2:{search.Query}:{search.City}:{search.Category}:{search.MinBudget}:{search.MaxBudget}:{FormatDate(search.DeadlineFrom)}:{FormatDate(search.DeadlineTo)}:{search.Sort}:{search.Page}:{search.PageSize}";
        var cached = await cache.GetAsync<CampaignCatalogResult>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await catalog.SearchAsync(search, cancellationToken);
        await cache.SetAsync(key, result, CacheTtl, cancellationToken);
        return result;
    }

    internal static CampaignCatalogSearch Normalize(SearchCampaignCatalogQuery query) => new(
        NormalizeText(query.Query),
        NormalizeIdentifier(query.City),
        NormalizeIdentifier(query.Category),
        query.MinBudget,
        query.MaxBudget,
        query.DeadlineFrom,
        query.DeadlineTo,
        query.Sort?.Trim().ToLowerInvariant() ?? "promoted",
        query.Page,
        query.PageSize);

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static string? NormalizeIdentifier(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static string FormatDate(DateTime? value) => value?.ToString("O", CultureInfo.InvariantCulture) ?? "all";
}
