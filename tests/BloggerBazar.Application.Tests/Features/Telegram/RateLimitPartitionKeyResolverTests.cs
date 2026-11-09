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
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10, 200_000);
        var context = CreateContext("203.0.113.10", "tma valid-init-data");

        var partition = resolver.ResolveApiPartitionKey(context);

        Assert.Equal($"telegram:{5044343262 % 200_000}", partition);
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
    public void Caps_ip_partitions_and_keeps_signed_in_people_in_buckets_without_an_overflow()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 1, 1000);

        var firstIpPartition = resolver.ResolveApiPartitionKey(CreateContext("203.0.113.10"));
        var secondIpPartition = resolver.ResolveApiPartitionKey(CreateContext("203.0.113.11"));
        var telegramPartition = resolver.ResolveApiPartitionKey(CreateContext("203.0.113.12", "tma valid-init-data"));

        Assert.Equal("api:ip:203.0.113.10", firstIpPartition);
        Assert.Equal("api:ip:ip-overflow", secondIpPartition);
        Assert.Equal($"telegram:{5044343262 % 1000}", telegramPartition);
        Assert.DoesNotContain("overflow", telegramPartition);
    }

    [Fact]
    public void Web_admin_sign_in_uses_the_address_the_proxy_forwarded()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10, 10);
        var viaPrivateProxy = CreateContext("fd12:3456::7");
        viaPrivateProxy.Request.Headers["X-Forwarded-For"] = "198.51.100.7, 100.64.0.2";
        var viaCarrierNat = CreateContext("100.64.12.1");
        viaCarrierNat.Request.Headers["X-Forwarded-For"] = "198.51.100.8";

        Assert.Equal(SignInKey(resolver, "198.51.100.7"), resolver.ResolveForwardedIpPartitionKey(viaPrivateProxy, "admin-web-sign-in"));
        Assert.Equal(SignInKey(resolver, "198.51.100.8"), resolver.ResolveForwardedIpPartitionKey(viaCarrierNat, "admin-web-sign-in"));
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

        Assert.Equal(SignInKey(resolver, "203.0.113.10"), resolver.ResolveForwardedIpPartitionKey(direct, "admin-web-sign-in"));
        Assert.Equal(SignInKey(resolver, "10.0.0.5"), resolver.ResolveForwardedIpPartitionKey(garbage, "admin-web-sign-in"));
    }

    // Audit 2026-10-22: many addresses (a proxy pool) never fill the partitions and push newcomers into one shared
    // budget; each address keeps its own bucket, and the number of buckets stays fixed.
    [Fact]
    public void Forwarded_addresses_never_overflow_into_one_shared_budget()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 64, 10);
        var keys = new HashSet<string>();
        for (var index = 0; index < 5_000; index++)
        {
            var context = CreateContext("10.0.0.2");
            context.Request.Headers["X-Forwarded-For"] = $"198.{index / 65536 % 256}.{index / 256 % 256}.{index % 256}";
            var key = resolver.ResolveForwardedIpPartitionKey(context, "admin-web-sign-in");
            Assert.DoesNotContain("overflow", key);
            keys.Add(key);
        }

        Assert.InRange(keys.Count, 2, 64);
        var admin = CreateContext("10.0.0.2");
        admin.Request.Headers["X-Forwarded-For"] = "203.0.113.77";
        var again = CreateContext("10.0.0.2");
        again.Request.Headers["X-Forwarded-For"] = "203.0.113.77";
        Assert.Equal(resolver.ResolveForwardedIpPartitionKey(admin, "admin-web-sign-in"), resolver.ResolveForwardedIpPartitionKey(again, "admin-web-sign-in"));
    }

    [Fact]
    public void Web_admin_requests_get_a_budget_per_person_behind_the_admin_site_proxy()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10_000, 200_000);
        var first = CreateContext("fd12::1");
        first.Request.Path = "/api/admin/users";
        first.Request.Headers.Cookie = "__Host-bb_admin=abc";
        first.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        var second = CreateContext("fd12::1");
        second.Request.Path = "/api/admin-web/session";
        second.Request.Headers.Cookie = "__Host-bb_admin=def";
        second.Request.Headers["X-Forwarded-For"] = "198.51.100.9";
        var miniApp = CreateContext("fd12::1", "tma valid-init-data");
        miniApp.Request.Path = "/api/admin/users";
        miniApp.Request.Headers.Cookie = "__Host-bb_admin=abc";

        var firstKey = resolver.ResolveApiPartitionKey(first);
        Assert.StartsWith("admin-web:ip:bucket-", firstKey);
        Assert.Equal(firstKey.Replace("admin-web:", "admin-web-sign-in:"), SignInKey(resolver, "198.51.100.7"));
        Assert.Equal(SignInKey(resolver, "198.51.100.9").Replace("admin-web-sign-in:", "admin-web:"), resolver.ResolveApiPartitionKey(second));
        Assert.Equal($"telegram:{5044343262 % 200_000}", resolver.ResolveApiPartitionKey(miniApp));
    }

    [Fact]
    public void An_admin_cookie_outside_admin_paths_does_not_choose_the_budget()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10, 10);
        var marketplace = CreateContext("fd12::1");
        marketplace.Request.Path = "/api/bloggers";
        marketplace.Request.Headers.Cookie = "__Host-bb_admin=x";
        marketplace.Request.Headers["X-Forwarded-For"] = "198.51.100.7";

        Assert.Equal("api:ip:fd12::1", resolver.ResolveApiPartitionKey(marketplace));
    }

    // D64 stage B: a company's browsers come through the Mini App's nginx; each office address gets the browser budget
    // instead of all of them sharing the proxy's one.
    [Fact]
    public void Browser_requests_get_a_budget_per_address_behind_the_mini_app_proxy()
    {
        var resolver = new RateLimitPartitionKeyResolver(new FakeTelegramValidator(), 10_000, 200_000);
        var office = CreateContext("fd12::1");
        office.Request.Path = "/api/deals/me";
        office.Request.Headers.Cookie = "__Host-bb_web=abc";
        office.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        var signingIn = CreateContext("fd12::1");
        signingIn.Request.Path = "/api/web/sign-in/check";
        signingIn.Request.Headers.Cookie = "__Host-bb_web_sign_in=code.secret";
        signingIn.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
        var miniApp = CreateContext("fd12::1", "tma valid-init-data");
        miniApp.Request.Headers.Cookie = "__Host-bb_web=abc";

        var officeKey = resolver.ResolveApiPartitionKey(office);
        Assert.StartsWith($"{RateLimitPartitionKeyResolver.BrowserScope}:ip:bucket-", officeKey);
        Assert.Equal(officeKey, resolver.ResolveApiPartitionKey(signingIn));
        Assert.Equal($"telegram:{5044343262 % 200_000}", resolver.ResolveApiPartitionKey(miniApp));
    }

    private static string SignInKey(RateLimitPartitionKeyResolver resolver, string address) =>
        resolver.ResolveForwardedIpPartitionKey(CreateContext(address), "admin-web-sign-in");

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
