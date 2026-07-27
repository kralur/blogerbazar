using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BloggerBazar.Infrastructure.Caching;

public sealed class DistributedCacheHealthCheck(IDistributedCache cache, IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
        {
            return HealthCheckResult.Healthy("Distributed memory cache is active.");
        }

        try
        {
            var key = $"health:{Guid.NewGuid():N}";
            await cache.SetStringAsync(key, "ok", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            }, cancellationToken);
            var value = await cache.GetStringAsync(key, cancellationToken);
            await cache.RemoveAsync(key, cancellationToken);

            return value == "ok"
                ? HealthCheckResult.Healthy("Redis cache is available.")
                : HealthCheckResult.Unhealthy("Redis cache probe returned an unexpected value.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis cache is unavailable.", exception);
        }
    }
}
