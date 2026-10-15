using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Features.AdminWeb;

// The web admin rules that hold before any database access: switched off without an origin, the admin site origin
// for every change, signature checks, the sign-in rate limit, and the cookie never counting outside admin actions.
[Collection(BloggerBazar.Application.Tests.Integration.ApiHostCollection.Name)]
public sealed class AdminWebApiTests
{
    private const string BotToken = "123456:admin-web-api-test";
    private const string AdminSite = "https://admin.example.test";

    [Fact]
    public async Task Web_sign_in_is_off_until_the_admin_site_is_configured()
    {
        using var factory = new TestApiFactory(origin: null);
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin-web/config")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SignInAsync(client, LoginWidgetTestData.Signed(BotToken, 42), AdminSite)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin-web/session")).StatusCode);
    }

    [Fact]
    public async Task Config_names_the_bot_for_the_widget()
    {
        using var factory = new TestApiFactory();
        using var client = CreateClient(factory);

        var config = await client.GetFromJsonAsync<JsonElement>("/api/admin-web/config");

        Assert.Equal("BloggerBazarTestBot", config.GetProperty("botUsername").GetString());
    }

    [Fact]
    public async Task Sign_in_and_logout_are_accepted_only_from_the_admin_site()
    {
        using var factory = new TestApiFactory();
        using var client = CreateClient(factory);
        var fields = LoginWidgetTestData.Signed(BotToken, 42);

        var noOrigin = await SignInAsync(client, fields, origin: null);
        var otherSite = await SignInAsync(client, fields, "https://evil.example");
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/admin-web/logout");
        logout.Headers.Add("Origin", "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, noOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherSite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(logout)).StatusCode);
        Assert.False(noOrigin.Headers.Contains("Set-Cookie"));
    }

    // Kestrel stops a body over 16 KB with 413 before reading it; the test server has no body size feature, so here the
    // same request is stopped by validation (422). Either way no session is opened.
    [Fact]
    public async Task An_oversized_sign_in_body_is_refused()
    {
        using var factory = new TestApiFactory();
        using var client = CreateClient(factory);
        var huge = new Dictionary<string, string> { ["id"] = "42", ["hash"] = new string('a', 64), ["padding"] = new string('x', 20_000) };

        var response = await SignInAsync(client, huge, AdminSite);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.UnprocessableEntity });
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Forged_or_stale_widget_data_is_refused()
    {
        using var factory = new TestApiFactory();
        using var client = CreateClient(factory);
        var forged = LoginWidgetTestData.Signed("999:not-our-bot", 42);
        var stale = LoginWidgetTestData.Signed(BotToken, 42, DateTimeOffset.UtcNow.AddMinutes(-6));
        var tampered = LoginWidgetTestData.Signed(BotToken, 42);
        tampered["id"] = "43";

        foreach (var fields in new[] { forged, stale, tampered })
        {
            var response = await SignInAsync(client, fields, AdminSite);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("authentication_required", problem.GetProperty("code").GetString());
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
    }

    [Fact]
    public async Task Sign_in_attempts_are_rate_limited_per_address()
    {
        using var factory = new TestApiFactory(signInPermitLimit: 2);
        using var client = CreateClient(factory);
        var forged = LoginWidgetTestData.Signed("999:not-our-bot", 42);

        // Every spelling of the URL shares one budget (the limit is attached to the endpoint, not to a path).
        var first = await SignInAsync(client, forged, AdminSite);
        var second = await SignInAsync(client, forged, AdminSite, "/api/v1/admin-web/session/");
        var third = await SignInAsync(client, forged, AdminSite, "/API/admin-web/session/");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task The_cookie_never_counts_outside_admin_actions_and_an_unknown_one_counts_nowhere()
    {
        using var factory = new TestApiFactory();
        using var client = CreateClient(factory);
        const string cookie = "__Host-bb_admin=0123456789abcdefghijABCDEFGHIJ0123456789abc";

        using var marketplace = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        marketplace.Headers.Add("Cookie", cookie);
        using var admin = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        admin.Headers.Add("Cookie", cookie);
        using var session = new HttpRequestMessage(HttpMethod.Get, "/api/admin-web/session");
        session.Headers.Add("Cookie", cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(marketplace)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(admin)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(session)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
    }

    [Fact]
    public async Task A_change_with_the_cookie_from_another_site_is_refused_before_the_session_is_read()
    {
        using var factory = new TestApiFactory();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/admin/users/42/blocked") { Content = JsonContent.Create(new { isBlocked = true }) };
        request.Headers.Add("Cookie", "__Host-bb_admin=0123456789abcdefghijABCDEFGHIJ0123456789abc");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // The whole cookie path through the real HTTP pipeline, with the database replaced by fakes
    // (the same flow runs on Postgres in AdminWebSessionIntegrationTests).
    [Fact]
    public async Task Sign_in_cookie_admin_request_demotion_and_logout_work_end_to_end()
    {
        var access = new SwitchableAccess(42);
        var audit = new RecordingAuditLogs();
        using var factory = new TestApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAdminAccessPolicy>(access);
            services.AddSingleton<IPlatformUserRepository>(new SingleUser(42, PlatformRole.Owner));
            services.AddSingleton<IAuditLogRepository>(audit);
            services.AddSingleton<IUnitOfWork>(new SpyUnitOfWork());
        }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var signIn = await SignInAsync(client, LoginWidgetTestData.Signed(BotToken, 42), AdminSite);
        var setCookie = Assert.Single(signIn.Headers.GetValues("Set-Cookie"));
        var cookie = setCookie.Split(';')[0];
        var session = await signIn.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal("owner", session.GetProperty("role").GetString());
        Assert.StartsWith("__Host-bb_admin=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await SendWithCookieAsync(client, HttpMethod.Get, "/api/admin/csp-reports", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendWithCookieAsync(client, HttpMethod.Get, "/api/admin-web/session", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendWithCookieAsync(client, HttpMethod.Get, "/api/users/me", cookie)).StatusCode);

        access.Revoke(42);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendWithCookieAsync(client, HttpMethod.Get, "/api/admin/csp-reports", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendWithCookieAsync(client, HttpMethod.Get, "/api/admin-web/session", cookie)).StatusCode);

        access.Allow(42);
        var second = (await SignInAsync(client, LoginWidgetTestData.Signed(BotToken, 42, extra: new Dictionary<string, string> { ["photo_url"] = "https://t.me/i/userpic/320/x.jpg" }), AdminSite))
            .Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/admin-web/logout");
        logout.Headers.Add("Cookie", second);
        logout.Headers.Add("Origin", AdminSite);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendWithCookieAsync(client, HttpMethod.Get, "/api/admin/csp-reports", second)).StatusCode);
        Assert.Equal(["admin-web.signed-in", "admin-web.signed-in", "admin-web.signed-out"], audit.Actions);
    }

    private static async Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, HttpMethod method, string path, string cookie)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    internal static Task<HttpResponseMessage> SignInAsync(HttpClient client, IReadOnlyDictionary<string, string> fields, string? origin, string path = "/api/admin-web/session")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(fields) };
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return client.SendAsync(request);
    }

    private static HttpClient CreateClient(TestApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private sealed class SwitchableAccess(long allowed) : IAdminAccessPolicy
    {
        private volatile bool isAllowed = true;
        public void Revoke(long telegramUserId) => isAllowed = false;
        public void Allow(long telegramUserId) => isAllowed = true;
        public void EnsureAllowed(long telegramUserId)
        {
            if (!isAllowed || telegramUserId != allowed) throw new UnauthorizedAccessException("Administrator access is required.");
        }
    }

    private sealed class SingleUser(long telegramUserId, PlatformRole role) : IPlatformUserRepository
    {
        private readonly PlatformUser user = Create(telegramUserId, role);
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult(id == telegramUserId ? user : null);
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([user]);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public Task AddAsync(PlatformUser added, CancellationToken cancellationToken) => Task.CompletedTask;

        private static PlatformUser Create(long id, PlatformRole role)
        {
            var created = PlatformUser.Create(id, "Umid", "umid");
            created.SetRole(role);
            return created;
        }
    }

    private sealed class RecordingAuditLogs : IAuditLogRepository
    {
        private readonly List<string> actions = [];
        public string[] Actions { get { lock (actions) return [.. actions]; } }
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { lock (actions) actions.Add(entry.Action); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>([]);
    }

    private sealed class TestApiFactory(string? origin = AdminSite, int signInPermitLimit = 50) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=bloggerbazar_test;Username=postgres;Password=test",
                ["ConnectionStrings:Redis"] = string.Empty,
                ["Telegram:BotToken"] = BotToken,
                ["Telegram:BotUsername"] = "@BloggerBazarTestBot",
                ["Telegram:WebhookSecret"] = "test-webhook-secret",
                ["AdminWeb:Origin"] = origin ?? string.Empty,
                ["RateLimiting:AdminWebSignIn:PermitLimit"] = signInPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["DevelopmentData:Seed"] = "false",
                ["DealReminders:Enabled"] = "false",
                ["BotTips:Enabled"] = "false"
            })
            {
                builder.UseSetting(key, value);
            }
        }
    }
}
