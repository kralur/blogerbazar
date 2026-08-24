using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.BrandFaces;

public sealed record SearchBrandFaceCatalogQuery(
    string? Query,
    string? City,
    string? Category,
    string? Language,
    int? MinPrice,
    int? MaxPrice,
    string? Sort = "promoted",
    int Page = 1,
    int PageSize = 20) : IRequest<BrandFaceCatalogResult>;

public sealed record BrandFaceCatalogItemDto(
    Guid Id,
    string Name,
    string City,
    IReadOnlyCollection<string> Languages,
    IReadOnlyCollection<string> Categories,
    int? CollaborationPrice,
    string? AvatarUrl,
    bool IsPromoted,
    DateTime CreatedAtUtc);

public sealed record BrandFaceCatalogResult(
    IReadOnlyList<BrandFaceCatalogItemDto> Items,
    int Total,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record BrandFaceCatalogSearch(
    string? Query,
    string? City,
    string? Category,
    string? Language,
    int? MinPrice,
    int? MaxPrice,
    string Sort,
    int Page,
    int PageSize);

public sealed class SearchBrandFaceCatalogValidator : AbstractValidator<SearchBrandFaceCatalogQuery>
{
    private static readonly string[] Sorts = ["promoted", "newest", "price_asc", "price_desc"];

    public SearchBrandFaceCatalogValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
        RuleFor(query => query.Query).MaximumLength(100).When(query => query.Query is not null);
        RuleFor(query => query.City).MaximumLength(80).When(query => query.City is not null);
        RuleFor(query => query.Category).MaximumLength(50).When(query => query.Category is not null);
        RuleFor(query => query.Language).MaximumLength(32).When(query => query.Language is not null);
        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0).When(query => query.MinPrice.HasValue);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0).When(query => query.MaxPrice.HasValue);
        RuleFor(query => query).Must(query => !query.MinPrice.HasValue || !query.MaxPrice.HasValue || query.MinPrice <= query.MaxPrice)
            .WithMessage("Minimum price must not exceed maximum price.");
        RuleFor(query => query.Sort)
            .Must(sort => !string.IsNullOrWhiteSpace(sort) && Sorts.Contains(sort.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Sort must be one of: promoted, newest, price_asc, price_desc.");
    }
}

public sealed class SearchBrandFaceCatalogHandler(IBrandFaceCatalogReadModel catalog, ICatalogCache cache)
    : IRequestHandler<SearchBrandFaceCatalogQuery, BrandFaceCatalogResult>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    public async Task<BrandFaceCatalogResult> Handle(SearchBrandFaceCatalogQuery query, CancellationToken cancellationToken)
    {
        var search = Normalize(query);
        var namespaceVersion = await cache.GetNamespaceVersionAsync(cancellationToken);
        var key = $"catalog:{namespaceVersion}:brand-faces:{search.Query}:{search.City}:{search.Category}:{search.Language}:{search.MinPrice}:{search.MaxPrice}:{search.Sort}:{search.Page}:{search.PageSize}";
        var cached = await cache.GetAsync<BrandFaceCatalogResult>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await catalog.SearchAsync(search, cancellationToken);
        await cache.SetAsync(key, result, CacheTtl, cancellationToken);
        return result;
    }

    internal static BrandFaceCatalogSearch Normalize(SearchBrandFaceCatalogQuery query) => new(
        NormalizeText(query.Query),
        NormalizeIdentifier(query.City),
        NormalizeIdentifier(query.Category),
        NormalizeIdentifier(query.Language),
        query.MinPrice,
        query.MaxPrice,
        query.Sort?.Trim().ToLowerInvariant() ?? "promoted",
        query.Page,
        query.PageSize);

    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static string? NormalizeIdentifier(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
