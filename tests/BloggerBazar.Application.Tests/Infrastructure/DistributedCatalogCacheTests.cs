using BloggerBazar.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace BloggerBazar.Application.Tests.Infrastructure;

// Pre-launch audit 2026-11-04: a Redis outage turns the cache into misses, never into errors for catalog requests.
public sealed class DistributedCatalogCacheTests
{
    [Fact]
    public async Task A_broken_cache_reads_as_a_miss_and_writes_are_skipped()
    {
        var cache = new DistributedCatalogCache(new BrokenCache());

        Assert.Null(await cache.GetAsync<string[]>("key", CancellationToken.None));
        Assert.Equal("0", await cache.GetNamespaceVersionAsync(CancellationToken.None));
        Assert.Equal("0", await cache.GetNamespaceVersionAsync("campaigns", CancellationToken.None));
        await cache.SetAsync("key", new[] { "value" }, TimeSpan.FromMinutes(1), CancellationToken.None);
        await cache.RotateNamespaceVersionAsync(CancellationToken.None);
        await cache.RotateNamespaceVersionAsync("campaigns", CancellationToken.None);
    }

    private sealed class BrokenCache : IDistributedCache
    {
        private static Exception Down() => new InvalidOperationException("It was not possible to connect to the redis server(s).");
        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
    }
}
