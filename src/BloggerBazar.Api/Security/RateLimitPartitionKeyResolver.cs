using BloggerBazar.Application.Abstractions.Security;

namespace BloggerBazar.Api.Security;

internal sealed class RateLimitPartitionKeyResolver(
    ITelegramWebAppValidator telegramValidator,
    int maxIpPartitions,
    int maxTelegramUserPartitions)
{
    public const string PartitionKindItemKey = "rate-limit-partition-kind";

    private readonly object ipLock = new();
    private readonly object telegramUserLock = new();
    private readonly HashSet<string> ipPartitions = new(StringComparer.Ordinal);
    private readonly HashSet<string> telegramUserPartitions = new(StringComparer.Ordinal);

    public string ResolveApiPartitionKey(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        const string scheme = "tma ";
        if (authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var telegramUser = telegramValidator.Validate(authorization[scheme.Length..]);
                if (telegramUser.Id > 0)
                {
                    var key = ResolveBoundedKey(telegramUserPartitions, telegramUserLock, telegramUser.Id.ToString(), maxTelegramUserPartitions, "telegram-overflow");
                    context.Items[PartitionKindItemKey] = key == "telegram-overflow" ? "telegram-overflow" : "telegram";
                    return $"telegram:{key}";
                }
            }
            catch
            {
            }
        }

        return ResolveIpPartitionKey(context, "api");
    }

    public string ResolveIpPartitionKey(HttpContext context, string scope)
    {
        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = ResolveBoundedKey(ipPartitions, ipLock, address, maxIpPartitions, "ip-overflow");
        context.Items[PartitionKindItemKey] = key == "ip-overflow" ? "ip-overflow" : "ip";
        return $"{scope}:ip:{key}";
    }

    private static string ResolveBoundedKey(HashSet<string> partitions, object sync, string candidate, int capacity, string overflowKey)
    {
        lock (sync)
        {
            if (partitions.Contains(candidate))
            {
                return candidate;
            }

            if (partitions.Count >= capacity)
            {
                return overflowKey;
            }

            partitions.Add(candidate);
            return candidate;
        }
    }
}
