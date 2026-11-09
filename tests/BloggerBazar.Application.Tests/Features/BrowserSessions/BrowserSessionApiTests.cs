using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.BrowserSessions;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Application.Tests.Integration;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.BrowserSessions;

// D64 stage B through the real HTTP pipeline, with the database replaced by fakes (the same flow runs on Postgres in
// BrowserSessionIntegrationTests): the Mini App origin for every browser change, the sign-in, the cookie counting
// only on business actions, and the session ending when the person leaves the team.
[Collection(ApiHostCollection.Name)]
public sealed class BrowserSessionApiTests
{
    private const string MiniApp = "https://app.example.test";
    private const string SessionCookie = "__Host-bb_web";
    private const string SignInCookie = "__Host-bb_web_sign_in";
    private const long Owner = 22;
    private const long Manager = 77;
    private const long Outsider = 90;

    [Fact]
    public async Task Browser_sign_in_is_off_until_the_mini_app_address_is_configured()
    {
        using var factory = new TestApiFactory(new World(), miniAppUrl: null);
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await StartAsync(client, MiniApp)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Post, "/api/web/sign-in/check", origin: MiniApp)).StatusCode);
    }

    [Fact]
    public async Task Asking_to_sign_in_is_accepted_only_from_the_mini_app_page()
    {
        using var factory = new TestApiFactory(new World());
        using var client = CreateClient(factory);

        var noOrigin = await StartAsync(client, origin: null);
        var otherSite = await StartAsync(client, "https://evil.example");
        var start = await StartAsync(client, MiniApp + "/");
        var body = await start.Content.ReadFromJsonAsync<JsonElement>();
        var setCookie = Assert.Single(start.Headers.GetValues("Set-Cookie"));

        Assert.Equal(HttpStatusCode.Forbidden, noOrigin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherSite.StatusCode);
        Assert.False(otherSite.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var code = body.GetProperty("code").GetString()!;
        Assert.Equal($"https://t.me/BloggerBazarTestBot?startapp=web_{code}", body.GetProperty("link").GetString());
        Assert.InRange(body.GetProperty("number").GetInt32(), 10, 99);
        Assert.StartsWith($"{SignInCookie}={code}.", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
        // The secret stays in the cookie only.
        Assert.DoesNotContain(setCookie.Split(';')[0].Split('.')[1], await start.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manager_signs_a_browser_in_from_the_mini_app_and_the_cookie_works_only_on_business_actions()
    {
        var world = new World();
        using var factory = new TestApiFactory(world);
        using var browser = CreateClient(factory);
        using var miniApp = MiniAppClient(factory, Manager);

        var cookie = await SignInAsync(browser, miniApp);

        var list = await SendAsync(browser, HttpMethod.Get, "/api/web/sessions", cookie);
        var sessions = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.True(sessions[0].GetProperty("isCurrent").GetBoolean());
        Assert.Equal("chrome", sessions[0].GetProperty("browser").GetString());
        Assert.Equal("windows", sessions[0].GetProperty("os").GetString());
        // The same list in the Mini App, where no browser is the current one.
        var fromMiniApp = await miniApp.GetFromJsonAsync<JsonElement>("/api/v1/web/sessions");
        Assert.False(fromMiniApp[0].GetProperty("isCurrent").GetBoolean());

        // Not a business action: account deletion, role switch, approving browsers, applying as a blogger. The browser
        // hears "in Telegram only", not "sign in again".
        foreach (var (method, path, body) in new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Delete, "/api/users/me", null),
            (HttpMethod.Put, "/api/users/me/selected-role", new { role = "Blogger" }),
            (HttpMethod.Post, "/api/web/sign-in/preview", new { code = "x" }),
            (HttpMethod.Post, $"/api/campaigns/{Guid.NewGuid()}/applications", new { message = "Hi" })
        })
        {
            var refused = await SendAsync(browser, method, path, cookie, MiniApp, body);
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{method} {path} answered {(int)refused.StatusCode}");
            Assert.Equal("telegram_only", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(browser, HttpMethod.Get, "/api/admin/dashboard", cookie)).StatusCode);
        // The person's other roles do not exist for the browser.
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(browser, HttpMethod.Get, "/api/bloggers/me", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(browser, HttpMethod.Get, "/api/brand-faces/me", cookie)).StatusCode);
        Assert.Equal("[]", await (await SendAsync(browser, HttpMethod.Get, "/api/campaign-applications/me", cookie)).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(browser, HttpMethod.Put, "/api/users/me/roles/blogger/visibility", cookie, MiniApp, new { hidden = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(browser, HttpMethod.Delete, "/api/profile-media/blogger", cookie, MiniApp)).StatusCode);

        // A change with the cookie must come from the Mini App page.
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(browser, HttpMethod.Post, "/api/web/sessions/end-all", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(browser, HttpMethod.Post, "/api/web/sessions/end-all", cookie, "https://evil.example")).StatusCode);

        var endAll = await SendAsync(browser, HttpMethod.Post, "/api/web/sessions/end-all", cookie, MiniApp);
        Assert.Equal(HttpStatusCode.NoContent, endAll.StatusCode);
        Assert.StartsWith($"{SessionCookie}=;", Assert.Single(endAll.Headers.GetValues("Set-Cookie")), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(browser, HttpMethod.Get, "/api/web/sessions", cookie)).StatusCode);
        Assert.Equal([BrowserSessionAuditActions.SignedIn, BrowserSessionAuditActions.SignedOutEverywhere], world.Audit.Actions);
        Assert.Equal([Manager], world.Bot.NotifiedChats);
    }

    [Fact]
    public async Task Leaving_the_team_ends_the_browser_session_on_the_next_request()
    {
        var world = new World();
        using var factory = new TestApiFactory(world);
        using var browser = CreateClient(factory);
        using var miniApp = MiniAppClient(factory, Manager);
        var cookie = await SignInAsync(browser, miniApp);

        world.Team.Remove(Manager);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(browser, HttpMethod.Get, "/api/web/sessions", cookie)).StatusCode);
        world.Team.Add(Manager, BusinessMemberRole.Manager);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(browser, HttpMethod.Get, "/api/web/sessions", cookie)).StatusCode);
    }

    [Fact]
    public async Task Telegram_sign_in_wins_over_a_browser_cookie()
    {
        var world = new World();
        using var factory = new TestApiFactory(world);
        using var browser = CreateClient(factory);
        using var miniApp = MiniAppClient(factory, Manager);
        var cookie = await SignInAsync(browser, miniApp);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/web/sessions");
        request.Headers.Add("Cookie", cookie);
        request.Headers.Authorization = new("tma", BloggerBazarApiFactory.CreateInitData(Owner));
        var asOwner = await (await browser.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, asOwner.GetArrayLength());
    }

    [Fact]
    public async Task Somebody_outside_any_company_cannot_confirm_and_a_wrong_number_cancels()
    {
        var world = new World();
        using var factory = new TestApiFactory(world);
        using var browser = CreateClient(factory);
        using var outsider = MiniAppClient(factory, Outsider);
        using var owner = MiniAppClient(factory, Owner);
        var (code, number, pending) = await StartForCodeAsync(browser);

        var preview = await (await outsider.PostAsJsonAsync("/api/web/sign-in/preview", new { code })).Content.ReadFromJsonAsync<JsonElement>();
        var outsiderConfirm = await outsider.PostAsJsonAsync("/api/web/sign-in/confirm", new { code, number });
        var wrong = await owner.PostAsJsonAsync("/api/web/sign-in/confirm", new { code, number = number == 99 ? 98 : number + 1 });
        var retry = await owner.PostAsJsonAsync("/api/web/sign-in/confirm", new { code, number });
        var check = await (await SendAsync(browser, HttpMethod.Post, "/api/web/sign-in/check", pending, MiniApp)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(BrowserEligibility.NoCompany, preview.GetProperty("eligibility").GetString());
        Assert.Equal(HttpStatusCode.Conflict, outsiderConfirm.StatusCode);
        Assert.Equal(BrowserSessionCodes.CompanyRequired, (await outsiderConfirm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        Assert.Equal(BrowserSessionCodes.NumberMismatch, (await wrong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);
        Assert.Equal(BrowserSignInStatuses.Declined, check.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_sign_in_link_opened_from_a_chat_with_somebody_else_is_refused()
    {
        var world = new World();
        using var factory = new TestApiFactory(world);
        using var browser = CreateClient(factory);
        using var victim = CreateClient(factory);
        victim.DefaultRequestHeaders.Authorization = new("tma", BloggerBazarApiFactory.CreateInitData(Owner, chatType: "private"));
        var (code, number, pending) = await StartForCodeAsync(browser);

        var preview = await (await victim.PostAsJsonAsync("/api/web/sign-in/preview", new { code })).Content.ReadFromJsonAsync<JsonElement>();
        var confirm = await victim.PostAsJsonAsync("/api/web/sign-in/confirm", new { code, number });
        var check = await (await SendAsync(browser, HttpMethod.Post, "/api/web/sign-in/check", pending, MiniApp)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(BrowserEligibility.SharedLink, preview.GetProperty("eligibility").GetString());
        Assert.Equal(0, preview.GetProperty("options").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal(BrowserSessionCodes.LinkShared, (await confirm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(BrowserSignInStatuses.Declined, check.GetProperty("status").GetString());
        Assert.Empty(world.Bot.NotifiedChats);
    }

    [Fact]
    public async Task Asking_to_sign_in_is_rate_limited_per_address()
    {
        using var factory = new TestApiFactory(new World(), signInPermitLimit: 2);
        using var client = CreateClient(factory);

        Assert.Equal(HttpStatusCode.OK, (await StartAsync(client, MiniApp)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(client, MiniApp, "/api/v1/web/sign-in")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await StartAsync(client, MiniApp, "/API/web/sign-in/")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_cookie_counts_nowhere()
    {
        using var factory = new TestApiFactory(new World());
        using var client = CreateClient(factory);
        var forged = $"{SessionCookie}=0123456789abcdefghijABCDEFGHIJ0123456789abc";

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/web/sessions", forged)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/deals/me", forged)).StatusCode);
        var check = await (await SendAsync(client, HttpMethod.Post, "/api/web/sign-in/check", $"{SignInCookie}=abc.def", MiniApp)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(BrowserSignInStatuses.Expired, check.GetProperty("status").GetString());
    }

    // Browser asks, the Mini App previews and confirms, the browser checks back and gets the session cookie.
    private static async Task<string> SignInAsync(HttpClient browser, HttpClient miniApp)
    {
        var (code, number, pending) = await StartForCodeAsync(browser);
        var waiting = await (await SendAsync(browser, HttpMethod.Post, "/api/web/sign-in/check", pending, MiniApp)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(BrowserSignInStatuses.Pending, waiting.GetProperty("status").GetString());

        var preview = await (await miniApp.PostAsJsonAsync("/api/web/sign-in/preview", new { code })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(BrowserEligibility.Ok, preview.GetProperty("eligibility").GetString());
        Assert.Contains(number, preview.GetProperty("options").EnumerateArray().Select(option => option.GetInt32()));
        Assert.Equal(HttpStatusCode.NoContent, (await miniApp.PostAsJsonAsync("/api/web/sign-in/confirm", new { code, number })).StatusCode);

        var check = await SendAsync(browser, HttpMethod.Post, "/api/web/sign-in/check", pending, MiniApp);
        var status = await check.Content.ReadFromJsonAsync<JsonElement>();
        var cookies = check.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Equal(BrowserSignInStatuses.Approved, status.GetProperty("status").GetString());
        Assert.Contains(cookies, cookie => cookie.StartsWith($"{SignInCookie}=;", StringComparison.Ordinal));
        var session = Assert.Single(cookies, cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal) && !cookie.StartsWith($"{SessionCookie}=;", StringComparison.Ordinal));
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", session, StringComparison.OrdinalIgnoreCase);
        return session.Split(';')[0];
    }

    private static async Task<(string Code, int Number, string PendingCookie)> StartForCodeAsync(HttpClient browser)
    {
        var start = await StartAsync(browser, MiniApp);
        var body = await start.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("code").GetString()!, body.GetProperty("number").GetInt32(), Assert.Single(start.Headers.GetValues("Set-Cookie")).Split(';')[0]);
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, string? origin, string path = "/api/web/sign-in")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
        if (origin is not null) request.Headers.Add("Origin", origin);
        return client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? cookie = null, string? origin = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        if (origin is not null) request.Headers.Add("Origin", origin);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static HttpClient MiniAppClient(WebApplicationFactory<Program> factory, long telegramUserId)
    {
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Authorization = new("tma", BloggerBazarApiFactory.CreateInitData(telegramUserId));
        return client;
    }

    // Users, one company with a team, and recorders, shared by every request of one test.
    private sealed class World
    {
        public Users Users { get; } = new(User(Owner, MarketplaceRole.Business), User(Manager, MarketplaceRole.Business), User(Outsider, MarketplaceRole.Business));
        public Team Team { get; } = new(Business(Owner, "Kofe Hona"), new() { [Owner] = BusinessMemberRole.Owner, [Manager] = BusinessMemberRole.Manager });
        public RecordingAuditLogs Audit { get; } = new();
        public SpyBotClient Bot { get; } = new();
    }

    private sealed class Users(params PlatformUser[] users) : IPlatformUserRepository, IPlatformUserAccessPolicy
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Length);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;

        public void EnsureActive(long telegramUserId)
        {
            if (users.Any(user => user.TelegramUserId == telegramUserId && (user.IsBlocked || user.IsDeleted)))
            {
                throw new UnauthorizedAccessException("This Telegram account is not allowed to access the marketplace.");
            }
        }
    }

    private sealed class Team(BusinessProfile business, Dictionary<long, BusinessMemberRole> members) : IBusinessProfileRepository
    {
        private readonly object sync = new();
        public void Remove(long telegramUserId) { lock (sync) members.Remove(telegramUserId); }
        public void Add(long telegramUserId, BusinessMemberRole role) { lock (sync) members[telegramUserId] = role; }
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(id == business.Id ? business : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult<BusinessProfile?>(telegramUserId == business.TelegramUserId ? business : null);
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessMembership?> GetMembershipAsync(long telegramUserId, CancellationToken cancellationToken)
        {
            lock (sync) return Task.FromResult(members.TryGetValue(telegramUserId, out var role) ? new BusinessMembership(business, role) : null);
        }
    }

    private sealed class RecordingAuditLogs : IAuditLogRepository
    {
        private readonly List<string> actions = [];
        public string[] Actions { get { lock (actions) return [.. actions]; } }
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { lock (actions) actions.Add(entry.Action); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>([]);
    }

    private sealed class TestApiFactory(World world, string? miniAppUrl = MiniApp + "/", int signInPermitLimit = 100) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=bloggerbazar_test;Username=postgres;Password=test",
                ["ConnectionStrings:Redis"] = string.Empty,
                ["Telegram:BotToken"] = BloggerBazarApiFactory.BotToken,
                ["Telegram:BotUsername"] = "@BloggerBazarTestBot",
                ["Telegram:MiniAppUrl"] = miniAppUrl ?? string.Empty,
                ["Telegram:WebhookSecret"] = "test-webhook-secret",
                ["RateLimiting:BrowserSignIn:PermitLimit"] = signInPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["DevelopmentData:Seed"] = "false",
                ["DealReminders:Enabled"] = "false",
                ["BotTips:Enabled"] = "false"
            })
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPlatformUserRepository>(world.Users);
                services.AddSingleton<IPlatformUserAccessPolicy>(world.Users);
                services.AddSingleton<IBusinessProfileRepository>(world.Team);
                services.AddSingleton<IAuditLogRepository>(world.Audit);
                services.AddSingleton<IUnitOfWork>(new SpyUnitOfWork());
                services.AddSingleton<ITelegramBotClient>(world.Bot);
            });
        }
    }
}
