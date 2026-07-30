using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Campaigns;

public sealed record SearchCampaignsQuery(string? City, string? Category, int Page = 1, int PageSize = 20) : IRequest<IReadOnlyList<CampaignDto>>;

public sealed class SearchCampaignsValidator : AbstractValidator<SearchCampaignsQuery>
{
    public SearchCampaignsValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class SearchCampaignsHandler(IMarketplaceCatalogReadModel catalog, ICatalogCache cache) : IRequestHandler<SearchCampaignsQuery, IReadOnlyList<CampaignDto>>
{
    public async Task<IReadOnlyList<CampaignDto>> Handle(SearchCampaignsQuery query, CancellationToken cancellationToken)
    {
        var key = $"catalog:campaigns:{query.City?.Trim().ToLowerInvariant() ?? "all"}:{query.Category?.Trim().ToLowerInvariant() ?? "all"}:{query.Page}:{query.PageSize}";
        var cached = await cache.GetAsync<List<CampaignDto>>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var response = (await catalog.SearchCampaignsAsync(query.City, query.Category, (query.Page - 1) * query.PageSize, query.PageSize, cancellationToken)).ToList();
        await cache.SetAsync(key, response, TimeSpan.FromMinutes(1), cancellationToken);
        return response;
    }
}
