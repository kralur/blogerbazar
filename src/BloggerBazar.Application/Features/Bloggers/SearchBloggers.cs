using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Bloggers;

public sealed record SearchBloggersQuery(string? City, string? Category, int Page = 1, int PageSize = 20) : IRequest<IReadOnlyList<BloggerProfileDto>>;

public sealed class SearchBloggersValidator : AbstractValidator<SearchBloggersQuery>
{
    public SearchBloggersValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
        RuleFor(query => query.City).MaximumLength(80).When(query => query.City is not null);
        RuleFor(query => query.Category).MaximumLength(50).When(query => query.Category is not null);
    }
}

public sealed class SearchBloggersHandler(IBloggerProfileRepository profiles, ICatalogCache cache)
    : IRequestHandler<SearchBloggersQuery, IReadOnlyList<BloggerProfileDto>>
{
    public async Task<IReadOnlyList<BloggerProfileDto>> Handle(SearchBloggersQuery query, CancellationToken cancellationToken)
    {
        var key = $"catalog:bloggers:{query.City?.Trim().ToLowerInvariant() ?? "all"}:{query.Category?.Trim().ToLowerInvariant() ?? "all"}:{query.Page}:{query.PageSize}";
        var cached = await cache.GetAsync<List<BloggerProfileDto>>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var profilesPage = await profiles.SearchApprovedAsync(query.City, query.Category, (query.Page - 1) * query.PageSize, query.PageSize, cancellationToken);
        var response = profilesPage.Select(BloggerProfileDto.From).ToList();
        await cache.SetAsync(key, response, TimeSpan.FromMinutes(1), cancellationToken);
        return response;
    }
}
