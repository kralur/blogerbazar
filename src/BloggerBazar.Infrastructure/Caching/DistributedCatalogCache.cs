using System.Text.Json;
using BloggerBazar.Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace BloggerBazar.Infrastructure.Caching;

internal sealed class DistributedCatalogCache(IDistributedCache cache) : ICatalogCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private const string NamespaceVersionKey = "catalog:namespace-version";

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        var payload = await cache.GetStringAsync(key, cancellationToken);
        return payload is null ? null : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class =>
        cache.SetStringAsync(key, JsonSerializer.Serialize(value, SerializerOptions), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = timeToLive
        }, cancellationToken);

    public async Task<string> GetNamespaceVersionAsync(CancellationToken cancellationToken) =>
        await cache.GetStringAsync(NamespaceVersionKey, cancellationToken) ?? "0";

    public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) =>
        cache.SetStringAsync(NamespaceVersionKey, Guid.NewGuid().ToString("N"), new DistributedCacheEntryOptions(), cancellationToken);

    public async Task<string> GetNamespaceVersionAsync(string catalog, CancellationToken cancellationToken) =>
        await cache.GetStringAsync(CatalogNamespaceVersionKey(catalog), cancellationToken) ?? "0";

    public Task RotateNamespaceVersionAsync(string catalog, CancellationToken cancellationToken) =>
        cache.SetStringAsync(CatalogNamespaceVersionKey(catalog), Guid.NewGuid().ToString("N"), new DistributedCacheEntryOptions(), cancellationToken);

    private static string CatalogNamespaceVersionKey(string catalog) => $"catalog:{catalog}:namespace-version";
}
