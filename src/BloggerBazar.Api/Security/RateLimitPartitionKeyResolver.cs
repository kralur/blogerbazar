using System.Net;
using System.Net.Sockets;
using BloggerBazar.Application.Abstractions.Security;

namespace BloggerBazar.Api.Security;

internal sealed class RateLimitPartitionKeyResolver(
    ITelegramWebAppValidator telegramValidator,
    int maxIpPartitions,
    int maxTelegramUserPartitions)
{
    public const string PartitionKindItemKey = "rate-limit-partition-kind";

    private readonly object ipLock = new();
    private readonly HashSet<string> ipPartitions = new(StringComparer.Ordinal);

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
                    // Pre-launch audit 2026-11-04: a first-come list of ids filled up after maxTelegramUserPartitions
                    // people and then put every newcomer into one shared budget. Ids are signed by Telegram, so a fixed
                    // number of buckets by id keeps memory bounded without that cliff; below the bucket count every
                    // person has a budget of their own.
                    context.Items[PartitionKindItemKey] = "telegram";
                    return $"telegram:{telegramUser.Id % Math.Max(1, maxTelegramUserPartitions)}";
                }
            }
            catch
            {
            }
        }

        // The web admin reaches the API through the admin site's proxy: one budget per person, not one for the proxy.
        // Only on admin paths: anywhere else the cookie means nothing, so it must not pick the budget either.
        if (IsAdminPath(context.Request.Path) && AdminWebCookie.Read(context.Request) is not null)
        {
            return ResolveForwardedIpPartitionKey(context, "admin-web");
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

    // Behind a proxy (Railway's edge, the admin site's nginx) the connection comes from the proxy, and the edge writes
    // the caller's address first in X-Forwarded-For. The header is trusted only on a connection from a private network.
    // Audit 2026-10-22: forwarded addresses are real client addresses, so a first-come list would fill up from many
    // addresses (a proxy pool) and push every newcomer into one shared budget an attacker keeps empty. Instead each
    // address falls into one of a fixed number of buckets by a hash seeded per process (string hashes are randomized
    // in .NET), so the set never fills and nobody outside can aim at a given bucket. Idle limiters are dropped by
    // the rate limiter itself, so memory stays bounded.
    public string ResolveForwardedIpPartitionKey(HttpContext context, string scope)
    {
        var address = ForwardedClientAddress(context) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var bucket = (uint)StringComparer.Ordinal.GetHashCode(address) % (uint)Math.Max(1, maxIpPartitions);
        context.Items[PartitionKindItemKey] = "forwarded-ip";
        return $"{scope}:ip:bucket-{bucket}";
    }

    private static bool IsAdminPath(PathString path) =>
        path.StartsWithSegments("/api/admin") || path.StartsWithSegments("/api/admin-web")
        || path.StartsWithSegments("/api/v1/admin") || path.StartsWithSegments("/api/v1/admin-web");

    internal static string? ForwardedClientAddress(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (peer is null || !IsPrivateNetwork(peer))
        {
            return null;
        }

        var first = context.Request.Headers["X-Forwarded-For"].ToString().Split(',', 2)[0].Trim();
        return IPAddress.TryParse(first, out var client) ? client.ToString() : null;
    }

    internal static bool IsPrivateNetwork(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 && (address.IsIPv6UniqueLocal || address.IsIPv6LinkLocal);
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
