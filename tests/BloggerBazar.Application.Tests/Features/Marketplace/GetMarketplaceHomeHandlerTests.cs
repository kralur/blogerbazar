using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Marketplace;

namespace BloggerBazar.Application.Tests.Features.Marketplace;

public sealed class GetMarketplaceHomeHandlerTests
{
    private static readonly MarketplaceHomeDto Empty = new([], [], [], [], [], [], [], new MarketplaceStatisticsDto(0, 0, 0, 0, null));

    [Fact]
    public async Task Delegates_to_the_marketplace_read_model_and_keeps_the_answer()
    {
        var readModel = new StubMarketplaceHomeReadModel(Empty);
        var cache = new MemoryCatalogCache();
        var handler = new GetMarketplaceHomeHandler(readModel, cache);

        var result = await handler.Handle(new GetMarketplaceHomeQuery(), CancellationToken.None);

        Assert.Same(Empty, result);
        Assert.Equal(1, readModel.Calls);
        Assert.Equal(GetMarketplaceHomeHandler.CacheTime, Assert.Single(cache.TimesToLive));
    }

    // The home page is asked for on every start; the database answers once a minute, not once per person.
    [Fact]
    public async Task A_second_request_within_the_minute_is_served_from_the_cache()
    {
        var readModel = new StubMarketplaceHomeReadModel(Empty);
        var cache = new MemoryCatalogCache();
        var handler = new GetMarketplaceHomeHandler(readModel, cache);

        await handler.Handle(new GetMarketplaceHomeQuery(), CancellationToken.None);
        var second = await handler.Handle(new GetMarketplaceHomeQuery(), CancellationToken.None);

        Assert.Same(Empty, second);
        Assert.Equal(1, readModel.Calls);
    }

    // A new or changed profile (profiles namespace) or campaign (campaigns namespace) must show up at once, not after
    // the minute: either rotation changes the key.
    [Theory]
    [InlineData(null)]
    [InlineData("campaigns")]
    public async Task A_rotated_namespace_version_reads_the_database_again(string? catalog)
    {
        var readModel = new StubMarketplaceHomeReadModel(Empty);
        var cache = new MemoryCatalogCache();
        var handler = new GetMarketplaceHomeHandler(readModel, cache);

        await handler.Handle(new GetMarketplaceHomeQuery(), CancellationToken.None);
        if (catalog is null) await cache.RotateNamespaceVersionAsync(CancellationToken.None);
        else await cache.RotateNamespaceVersionAsync(catalog, CancellationToken.None);
        await handler.Handle(new GetMarketplaceHomeQuery(), CancellationToken.None);

        Assert.Equal(2, readModel.Calls);
        Assert.Equal(2, cache.Keys.Distinct().Count());
    }

    private sealed class StubMarketplaceHomeReadModel(MarketplaceHomeDto result) : IMarketplaceHomeReadModel
    {
        public int Calls { get; private set; }

        public Task<MarketplaceHomeDto> GetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class MemoryCatalogCache : ICatalogCache
    {
        private readonly Dictionary<string, object> entries = [];
        private readonly Dictionary<string, int> versions = [];

        public List<string> Keys { get; } = [];
        public List<TimeSpan> TimesToLive { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
        {
            Keys.Add(key);
            return Task.FromResult<T?>(entries.TryGetValue(key, out var value) ? (T)value : null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class
        {
            entries[key] = value;
            TimesToLive.Add(timeToLive);
            return Task.CompletedTask;
        }

        public Task<string> GetNamespaceVersionAsync(CancellationToken cancellationToken) => GetNamespaceVersionAsync("", cancellationToken);

        public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) => RotateNamespaceVersionAsync("", cancellationToken);

        public Task<string> GetNamespaceVersionAsync(string catalog, CancellationToken cancellationToken) =>
            Task.FromResult(versions.GetValueOrDefault(catalog).ToString());

        public Task RotateNamespaceVersionAsync(string catalog, CancellationToken cancellationToken)
        {
            versions[catalog] = versions.GetValueOrDefault(catalog) + 1;
            return Task.CompletedTask;
        }
    }
}
