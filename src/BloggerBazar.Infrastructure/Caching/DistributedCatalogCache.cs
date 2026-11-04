using System.Text.Json;
using BloggerBazar.Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Infrastructure.Caching;

// Pre-launch audit 2026-11-04: the cache only speeds things up, so a Redis outage or timeout must not turn every
// catalog request into a 500: a failed read is a miss, a failed write or rotation is logged and skipped.
internal sealed class DistributedCatalogCache(IDistributedCache cache, ILogger<DistributedCatalogCache>? logger = null) : ICatalogCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private const string NamespaceVersionKey = "catalog:namespace-version";

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        var payload = await ReadAsync(key, cancellationToken);
        return payload is null ? null : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class =>
        WriteAsync(key, JsonSerializer.Serialize(value, SerializerOptions), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = timeToLive
        }, cancellationToken);

    public async Task<string> GetNamespaceVersionAsync(CancellationToken cancellationToken) =>
        await ReadAsync(NamespaceVersionKey, cancellationToken) ?? "0";

    public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) =>
        WriteAsync(NamespaceVersionKey, Guid.NewGuid().ToString("N"), new DistributedCacheEntryOptions(), cancellationToken);

    public async Task<string> GetNamespaceVersionAsync(string catalog, CancellationToken cancellationToken) =>
        await ReadAsync(CatalogNamespaceVersionKey(catalog), cancellationToken) ?? "0";

    public Task RotateNamespaceVersionAsync(string catalog, CancellationToken cancellationToken) =>
        WriteAsync(CatalogNamespaceVersionKey(catalog), Guid.NewGuid().ToString("N"), new DistributedCacheEntryOptions(), cancellationToken);

    private static string CatalogNamespaceVersionKey(string catalog) => $"catalog:{catalog}:namespace-version";

    private async Task<string?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try { return await cache.GetStringAsync(key, cancellationToken); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger?.LogWarning(exception, "Catalog cache read failed; answering from the database");
            return null;
        }
    }

    private async Task WriteAsync(string key, string value, DistributedCacheEntryOptions entry, CancellationToken cancellationToken)
    {
        try { await cache.SetStringAsync(key, value, entry, cancellationToken); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger?.LogWarning(exception, "Catalog cache write failed; the entry is skipped");
        }
    }
}
