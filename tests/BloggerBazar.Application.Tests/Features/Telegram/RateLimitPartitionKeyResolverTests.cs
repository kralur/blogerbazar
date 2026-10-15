using System.Net;
using BloggerBazar.Api.Security;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;

namespace BloggerBazar.Application.Tests.Features.Telegram;

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

    [Fact]
    public void Web_admin_sign_in_uses_the_address_the_proxy_forwarded()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10, 10);
        var viaPrivateProxy = CreateContext("fd12:3456::7");
        viaPrivateProxy.Request.Headers["X-Forwarded-For"] = "198.51.100.7, 100.64.0.2";
        var viaCarrierNat = CreateContext("100.64.12.1");
        viaCarrierNat.Request.Headers["X-Forwarded-For"] = "198.51.100.8";

        Assert.Equal("admin-web-sign-in:ip:198.51.100.7", resolver.ResolveForwardedIpPartitionKey(viaPrivateProxy, "admin-web-sign-in"));
        Assert.Equal("admin-web-sign-in:ip:198.51.100.8", resolver.ResolveForwardedIpPartitionKey(viaCarrierNat, "admin-web-sign-in"));
        Assert.Equal("forwarded-ip", viaPrivateProxy.Items[RateLimitPartitionKeyResolver.PartitionKindItemKey]);
    }

    [Fact]
    public void A_direct_caller_cannot_choose_its_address_with_a_forwarded_header()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10, 10);
        var direct = CreateContext("203.0.113.10");
        direct.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        var garbage = CreateContext("10.0.0.5");
        garbage.Request.Headers["X-Forwarded-For"] = "not-an-address";

        Assert.Equal("admin-web-sign-in:ip:203.0.113.10", resolver.ResolveForwardedIpPartitionKey(direct, "admin-web-sign-in"));
        Assert.Equal("admin-web-sign-in:ip:10.0.0.5", resolver.ResolveForwardedIpPartitionKey(garbage, "admin-web-sign-in"));
    }

    [Fact]
    public void Web_admin_requests_get_a_budget_per_person_behind_the_admin_site_proxy()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10, 10);
        var first = CreateContext("fd12::1");
        first.Request.Headers.Cookie = "__Host-bb_admin=abc";
        first.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        var second = CreateContext("fd12::1");
        second.Request.Headers.Cookie = "__Host-bb_admin=def";
        second.Request.Headers["X-Forwarded-For"] = "198.51.100.9";
        var miniApp = CreateContext("fd12::1", "tma valid-init-data");
        miniApp.Request.Headers.Cookie = "__Host-bb_admin=abc";

        Assert.Equal("admin-web:ip:198.51.100.7", resolver.ResolveApiPartitionKey(first));
        Assert.Equal("admin-web:ip:198.51.100.9", resolver.ResolveApiPartitionKey(second));
        Assert.Equal("telegram:5044343262", resolver.ResolveApiPartitionKey(miniApp));
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
