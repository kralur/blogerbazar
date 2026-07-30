using System.Net;
using BloggerBazar.Api.Security;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;

namespace BloggerBazar.Application.Tests.Features.Payments;

public sealed class RateLimitPartitionKeyResolverTests
{
    [Fact]
    public void Uses_authenticated_telegram_id_after_successful_validation()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10, 10);
        var context = CreateContext("203.0.113.10", "tma valid-init-data");

        var partition = resolver.ResolveApiPartitionKey(context);

        Assert.Equal("telegram:5044343262", partition);
        Assert.Equal("telegram", context.Items[RateLimitPartitionKeyResolver.PartitionKindItemKey]);
    }

    [Fact]
    public void Uses_ip_when_telegram_authorization_is_missing_or_invalid()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10, 10);
        var missingAuthorization = CreateContext("203.0.113.10");
        var invalidAuthorization = CreateContext("203.0.113.10", "tma invalid-init-data");

        var missingPartition = resolver.ResolveApiPartitionKey(missingAuthorization);
        var invalidPartition = resolver.ResolveApiPartitionKey(invalidAuthorization);

        Assert.Equal("api:ip:203.0.113.10", missingPartition);
        Assert.Equal(missingPartition, invalidPartition);
    }

    [Fact]
    public void Caps_partition_cardinality_and_uses_overflow_partitions()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 1, 1);

        var firstIpPartition = resolver.ResolveApiPartitionKey(CreateContext("203.0.113.10"));
        var secondIpPartition = resolver.ResolveApiPartitionKey(CreateContext("203.0.113.11"));
        var firstTelegramPartition = resolver.ResolveApiPartitionKey(CreateContext("203.0.113.12", "tma valid-init-data"));
        var secondTelegramPartition = resolver.ResolveApiPartitionKey(CreateContext("203.0.113.13", "tma valid-init-data-2"));

        Assert.Equal("api:ip:203.0.113.10", firstIpPartition);
        Assert.Equal("api:ip:ip-overflow", secondIpPartition);
        Assert.Equal("telegram:5044343262", firstTelegramPartition);
        Assert.Equal("telegram:telegram-overflow", secondTelegramPartition);
    }

    private static DefaultHttpContext CreateContext(string address, string? authorization = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        return context;
    }

    private sealed class FakeTelegramValidator : ITelegramWebAppValidator
    {
        public TelegramWebAppUser Validate(string initData) => initData switch
        {
            "valid-init-data" => new TelegramWebAppUser(5044343262, "umidkb", "Umid"),
            "valid-init-data-2" => new TelegramWebAppUser(5044343263, "user", "User"),
            _ => throw new UnauthorizedAccessException()
        };
    }
}
