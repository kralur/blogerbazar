using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;

namespace BloggerBazar.Application.Features.PublicStats;

// D58: the landing page shows three totals, nothing about any one person. Approved, not deleted profiles whose account
// is not blocked or deleted (the same people the catalogue shows), and completed deals.
public sealed record PublicStatsDto(int Bloggers, int Businesses, int CompletedDeals);

public sealed record GetPublicStatsQuery : IRequest<PublicStatsDto>;

public sealed class GetPublicStatsHandler(IPublicStatsReadModel stats, ICatalogCache cache) : IRequestHandler<GetPublicStatsQuery, PublicStatsDto>
{
    // Anyone can ask without signing in, so the counts run at most once per ten minutes, not once per visitor.
    public const string CacheKey = "public-stats:v1";
    public static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(10);

    public async Task<PublicStatsDto> Handle(GetPublicStatsQuery query, CancellationToken cancellationToken)
    {
        if (await cache.GetAsync<PublicStatsDto>(CacheKey, cancellationToken) is { } cached)
        {
            return cached;
        }

        var fresh = await stats.GetAsync(cancellationToken);
        await cache.SetAsync(CacheKey, fresh, CacheTime, cancellationToken);
        return fresh;
    }
}
