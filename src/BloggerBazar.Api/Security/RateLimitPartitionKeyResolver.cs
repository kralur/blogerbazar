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

        // The web admin reaches the API through the admin site's proxy: one budget per person, not one for the proxy.
        if (AdminWebCookie.Read(context.Request) is not null)
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
    public string ResolveForwardedIpPartitionKey(HttpContext context, string scope)
    {
        var address = ForwardedClientAddress(context) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = ResolveBoundedKey(ipPartitions, ipLock, address, maxIpPartitions, "ip-overflow");
        context.Items[PartitionKindItemKey] = key == "ip-overflow" ? "ip-overflow" : "forwarded-ip";
        return $"{scope}:ip:{key}";
    }

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
