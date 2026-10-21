using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.PublicStats;

namespace BloggerBazar.Application.Tests.Features.PublicStats;

public sealed class PublicStatsHandlerTests
{
    [Fact]
    public async Task Counts_once_and_answers_from_the_cache_for_ten_minutes()
    {
        var stats = new CountingStats(new PublicStatsDto(12, 5, 3));
        var cache = new MemoryCache();
        var handler = new GetPublicStatsHandler(stats, cache);

        var first = await handler.Handle(new GetPublicStatsQuery(), CancellationToken.None);
        var second = await handler.Handle(new GetPublicStatsQuery(), CancellationToken.None);

        Assert.Equal(new PublicStatsDto(12, 5, 3), first);
        Assert.Equal(first, second);
        Assert.Equal(1, stats.Calls);
        Assert.Equal((GetPublicStatsHandler.CacheKey, TimeSpan.FromMinutes(10)), cache.LastSet);
    }

    private sealed class CountingStats(PublicStatsDto result) : IPublicStatsReadModel
    {
        public int Calls { get; private set; }

        public Task<PublicStatsDto> GetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class MemoryCache : ICatalogCache
    {
        private readonly Dictionary<string, object> values = [];
        public (string Key, TimeSpan TimeToLive) LastSet { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class =>
            Task.FromResult(values.TryGetValue(key, out var value) ? (T)value : null);

        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class
        {
            values[key] = value;
            LastSet = (key, timeToLive);
            return Task.CompletedTask;
        }
    }
}
