using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Bloggers;

public sealed record SearchBloggersQuery(
    string? Query,
    string? City,
    string? Category,
    string? Platform,
    int? MinFollowers,
    int? MinEngagementRate,
    int? MaxEngagementRate,
    int? MinPrice,
    int? MaxPrice,
    string? Sort,
    int Page = 1,
    int PageSize = 20) : IRequest<SearchBloggersResult>;

public sealed record SearchBloggersResult(IReadOnlyList<BloggerProfileDto> Bloggers, int Total, int Page, int PageSize);

public sealed class SearchBloggersValidator : AbstractValidator<SearchBloggersQuery>
{
    public SearchBloggersValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
        RuleFor(query => query.Query).MaximumLength(100).When(query => query.Query is not null);
        RuleFor(query => query.City).MaximumLength(80).When(query => query.City is not null);
        RuleFor(query => query.Category).MaximumLength(50).When(query => query.Category is not null);
        RuleFor(query => query.Platform).MaximumLength(50).When(query => query.Platform is not null);
        RuleFor(query => query.MinFollowers).GreaterThanOrEqualTo(0).When(query => query.MinFollowers.HasValue);
        RuleFor(query => query.MinEngagementRate).InclusiveBetween(0, 100).When(query => query.MinEngagementRate.HasValue);
        RuleFor(query => query.MaxEngagementRate).InclusiveBetween(0, 100).When(query => query.MaxEngagementRate.HasValue);
        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0).When(query => query.MinPrice.HasValue);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0).When(query => query.MaxPrice.HasValue);
        RuleFor(query => query.Sort).Must(sort => sort is null or "popular" or "rating" or "er" or "price" or "newest");
    }
}

public sealed class SearchBloggersHandler(IMarketplaceCatalogReadModel catalog, ICatalogCache cache)
    : IRequestHandler<SearchBloggersQuery, SearchBloggersResult>
{
    public async Task<SearchBloggersResult> Handle(SearchBloggersQuery query, CancellationToken cancellationToken)
    {
        var key = $"catalog:bloggers:{query.Query}:{query.City}:{query.Category}:{query.Platform}:{query.MinFollowers}:{query.MinEngagementRate}:{query.MaxEngagementRate}:{query.MinPrice}:{query.MaxPrice}:{query.Sort}:{query.Page}:{query.PageSize}".ToLowerInvariant();
        var cached = await cache.GetAsync<SearchBloggersResult>(key, cancellationToken);
        if (cached is not null) return cached;

        var result = await catalog.SearchBloggersAsync(
            new BloggerCatalogSearch(query.Query, query.City, query.Category, query.Platform, query.MinFollowers,
                query.MinEngagementRate, query.MaxEngagementRate, query.MinPrice, query.MaxPrice, query.Sort, query.Page, query.PageSize),
            cancellationToken);
        await cache.SetAsync(key, result, TimeSpan.FromMinutes(1), cancellationToken);
        return result;
    }
}
