using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Application.Features.AdminWeb;
using BloggerBazar.Application.Tests.Features.AdminWeb;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

// Web admin sign-in end to end on a real database: Telegram Login Widget data in, an HttpOnly cookie out,
// the same Owner/Admin rule as the Mini App admin on every request.
[Collection(ApiHostCollection.Name)]
public sealed class AdminWebSessionIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    private const string CookieName = "__Host-bb_admin";

    [IntegrationFact]
    public async Task An_admin_signs_in_with_telegram_and_uses_the_admin_api_with_the_cookie()
    {
        await SeedUserAsync(1_300_001, PlatformRole.Admin);
        using var client = CreateClient();

        var signIn = await SignInAsync(client, 1_300_001);
        var body = await signIn.Content.ReadFromJsonAsync<JsonElement>();
        var setCookie = Assert.Single(signIn.Headers.GetValues("Set-Cookie"));
        var token = CookieValue(setCookie);

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal(AdminWebRoles.Admin, body.GetProperty("role").GetString());
        Assert.Equal(1_300_001, body.GetProperty("telegramUserId").GetInt64());
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, await signIn.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/admin/dashboard", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/v1/admin/users?take=5", token)).StatusCode);
        var session = await (await SendAsync(client, HttpMethod.Get, "/api/admin-web/session", token)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(AdminWebRoles.Admin, session.GetProperty("role").GetString());
        Assert.Contains(await AuditActionsAsync(1_300_001), action => action == AdminWebAuditActions.SignedIn);
    }

    [IntegrationFact]
    public async Task The_owner_is_reported_as_owner_and_changes_need_the_admin_site_origin()
    {
        await SeedUserAsync(1_300_011, PlatformRole.Owner);
        await SeedUserAsync(1_300_012, PlatformRole.Member);
        using var client = CreateClient();
        var signIn = await SignInAsync(client, 1_300_011);
        var token = CookieValue(Assert.Single(signIn.Headers.GetValues("Set-Cookie")));
        var path = "/api/admin/users/1300012/blocked";

        Assert.Equal(AdminWebRoles.Owner, (await signIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Patch, path, token, origin: null, new { isBlocked = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Patch, path, token, "https://evil.example", new { isBlocked = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Patch, path, token, BloggerBazarApiFactory.AdminWebOrigin, new { isBlocked = true })).StatusCode);
        Assert.Contains(await AuditActionsAsync(1_300_011), action => action == "platform-user.blocked");
    }

    [IntegrationFact]
    public async Task Members_blocked_and_deleted_admins_and_strangers_are_refused_and_recorded()
    {
        await SeedUserAsync(1_300_021, PlatformRole.Member);
        await SeedUserAsync(1_300_022, PlatformRole.Admin, blocked: true);
        await SeedUserAsync(1_300_023, PlatformRole.Admin, deleted: true);
        using var client = CreateClient();

        foreach (var telegramUserId in new long[] { 1_300_021, 1_300_022, 1_300_023, 1_300_024 })
        {
            var response = await SignInAsync(client, telegramUserId);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("access_denied", problem.GetProperty("code").GetString());
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }

        Assert.Contains(await AuditActionsAsync(1_300_021), action => action == AdminWebAuditActions.SignInDenied);
    }

    [IntegrationFact]
    public async Task Reused_stale_and_forged_sign_ins_are_refused()
    {
        await SeedUserAsync(1_300_031, PlatformRole.Admin);
        using var client = CreateClient();
        var fields = LoginWidgetTestData.Signed(BloggerBazarApiFactory.BotToken, 1_300_031);

        Assert.Equal(HttpStatusCode.OK, (await AdminWebApiTests.SignInAsync(client, fields, BloggerBazarApiFactory.AdminWebOrigin)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AdminWebApiTests.SignInAsync(client, fields, BloggerBazarApiFactory.AdminWebOrigin)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AdminWebApiTests.SignInAsync(client, LoginWidgetTestData.Signed(BloggerBazarApiFactory.BotToken, 1_300_031, DateTimeOffset.UtcNow.AddMinutes(-10)), BloggerBazarApiFactory.AdminWebOrigin)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AdminWebApiTests.SignInAsync(client, LoginWidgetTestData.Signed("999:other-bot", 1_300_031), BloggerBazarApiFactory.AdminWebOrigin)).StatusCode);
    }

    [IntegrationFact]
    public async Task Removing_the_role_or_blocking_ends_the_web_session_on_the_next_request()
    {
        await SeedUserAsync(1_300_041, PlatformRole.Admin);
        await SeedUserAsync(1_300_042, PlatformRole.Admin);
        using var client = CreateClient();
        var demoted = CookieValue(Assert.Single((await SignInAsync(client, 1_300_041)).Headers.GetValues("Set-Cookie")));
        var blocked = CookieValue(Assert.Single((await SignInAsync(client, 1_300_042)).Headers.GetValues("Set-Cookie")));

        await UpdateUserAsync(1_300_041, user => user.SetRole(PlatformRole.Member));
        await UpdateUserAsync(1_300_042, user => user.SetBlocked(true));

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, "/api/admin/dashboard", demoted)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin-web/session", demoted)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, "/api/admin-web/session", blocked)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin/dashboard", blocked)).StatusCode);

        // Getting the role back does not revive the old session: a new sign-in is needed.
        await UpdateUserAsync(1_300_041, user => user.SetRole(PlatformRole.Admin));
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin/dashboard", demoted)).StatusCode);
    }

    [IntegrationFact]
    public async Task Logout_ends_the_session_and_is_recorded()
    {
        await SeedUserAsync(1_300_051, PlatformRole.Admin);
        using var client = CreateClient();
        var token = CookieValue(Assert.Single((await SignInAsync(client, 1_300_051)).Headers.GetValues("Set-Cookie")));

        var logout = await SendAsync(client, HttpMethod.Post, "/api/admin-web/logout", token, BloggerBazarApiFactory.AdminWebOrigin);
        var cleared = Assert.Single(logout.Headers.GetValues("Set-Cookie"));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.StartsWith($"{CookieName}=;", cleared, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin-web/session", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin/dashboard", token)).StatusCode);
        Assert.Contains(await AuditActionsAsync(1_300_051), action => action == AdminWebAuditActions.SignedOut);
    }

    [IntegrationFact]
    public async Task The_cookie_is_not_a_marketplace_login()
    {
        await SeedUserAsync(1_300_061, PlatformRole.Admin);
        using var client = CreateClient();
        var token = CookieValue(Assert.Single((await SignInAsync(client, 1_300_061)).Headers.GetValues("Set-Cookie")));

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/users/me", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/deals/me", token)).StatusCode);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, long telegramUserId) =>
        AdminWebApiTests.SignInAsync(client, LoginWidgetTestData.Signed(BloggerBazarApiFactory.BotToken, telegramUserId), BloggerBazarApiFactory.AdminWebOrigin);

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, string? origin = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", $"{CookieName}={token}");
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static string CookieValue(string setCookie)
    {
        Assert.StartsWith($"{CookieName}=", setCookie, StringComparison.Ordinal);
        return setCookie[(CookieName.Length + 1)..].Split(';')[0];
    }

    private async Task SeedUserAsync(long telegramUserId, PlatformRole role, bool blocked = false, bool deleted = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var user = PlatformUser.Create(telegramUserId, "Admin web", null);
        user.SetRole(role);
        if (blocked) user.SetBlocked(true);
        if (deleted) user.SoftDelete(telegramUserId);
        dbContext.Add(user);
        await dbContext.SaveChangesAsync();
    }

    private async Task UpdateUserAsync(long telegramUserId, Action<PlatformUser> change)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        change(await dbContext.PlatformUsers.SingleAsync(user => user.TelegramUserId == telegramUserId));
        await dbContext.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> AuditActionsAsync(long actorTelegramUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        return await dbContext.AuditLogs.AsNoTracking().Where(entry => entry.ActorTelegramUserId == actorTelegramUserId).Select(entry => entry.Action).ToListAsync();
    }
}
