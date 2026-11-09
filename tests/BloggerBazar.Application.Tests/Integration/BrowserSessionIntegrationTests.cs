using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.BrowserSessions;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

// D64 stage B end to end on a real database: a team member signs a browser in from the Mini App and works for the
// company there with the team role's rights; leaving the team ends the browser at once.
[Collection(ApiHostCollection.Name)]
public sealed class BrowserSessionIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    private const string SessionCookie = "__Host-bb_web";
    private const string Origin = BloggerBazarApiFactory.MiniAppOrigin;

    [IntegrationFact]
    public async Task A_manager_works_for_the_company_in_a_browser_until_removed_from_the_team()
    {
        const long owner = 1_400_001;
        const long manager = 1_400_002;
        var companyId = await SeedCompanyAsync(owner, "Browser Coffee");
        await SeedMemberAsync(companyId, manager, BusinessMemberRole.Manager, invitedBy: owner);
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var miniApp = factory.CreateClientFor(manager);

        var cookie = await SignInAsync(browser, miniApp);

        var me = await (await SendAsync(browser, HttpMethod.Get, "/api/users/me", cookie)).Content.ReadFromJsonAsync<JsonElement>();
        var company = await SendAsync(browser, HttpMethod.Get, "/api/businesses/me", cookie);
        Assert.Equal(manager, me.GetProperty("telegramUserId").GetInt64());
        Assert.Equal(HttpStatusCode.OK, company.StatusCode);
        Assert.Equal("Browser Coffee", (await company.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(browser, HttpMethod.Get, "/api/deals/me", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(browser, HttpMethod.Get, "/api/campaigns/mine", cookie)).StatusCode);
        // The company's incoming applications (the list is scoped by the selected role, Business here).
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(browser, HttpMethod.Get, "/api/campaign-applications/me", cookie)).StatusCode);
        // The team role still limits the browser: a manager does not hide the company.
        var hide = await SendAsync(browser, HttpMethod.Put, "/api/users/me/roles/business/visibility", cookie, Origin, new { hidden = true });
        Assert.Equal(HttpStatusCode.Forbidden, hide.StatusCode);
        Assert.Contains(await AuditActionsAsync(manager), action => action == BrowserSessionAuditActions.SignedIn);

        await RemoveMemberAsync(companyId, manager);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(browser, HttpMethod.Get, "/api/businesses/me", cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(browser, HttpMethod.Get, "/api/web/sessions", cookie)).StatusCode);
    }

    [IntegrationFact]
    public async Task A_blogger_cannot_sign_a_browser_in()
    {
        const long blogger = 1_400_011;
        await SeedUserAsync(blogger, MarketplaceRole.Blogger);
        using var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var miniApp = factory.CreateClientFor(blogger);
        var (code, number, _) = await StartAsync(browser);

        var preview = await (await miniApp.PostAsJsonAsync("/api/web/sign-in/preview", new { code })).Content.ReadFromJsonAsync<JsonElement>();
        var confirm = await miniApp.PostAsJsonAsync("/api/web/sign-in/confirm", new { code, number });

        Assert.Equal(BrowserEligibility.NoCompany, preview.GetProperty("eligibility").GetString());
        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
    }

    private static async Task<string> SignInAsync(HttpClient browser, HttpClient miniApp)
    {
        var (code, number, pending) = await StartAsync(browser);
        Assert.Equal(HttpStatusCode.NoContent, (await miniApp.PostAsJsonAsync("/api/web/sign-in/confirm", new { code, number })).StatusCode);
        var check = await SendAsync(browser, HttpMethod.Post, "/api/web/sign-in/check", pending, Origin);
        Assert.Equal(BrowserSignInStatuses.Approved, (await check.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        return check.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal) && !cookie.StartsWith($"{SessionCookie}=;", StringComparison.Ordinal)).Split(';')[0];
    }

    private static async Task<(string Code, int Number, string Pending)> StartAsync(HttpClient browser)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/web/sign-in");
        request.Headers.Add("Origin", Origin);
        var start = await browser.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var body = await start.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("code").GetString()!, body.GetProperty("number").GetInt32(), start.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string cookie, string? origin = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);
        if (origin is not null) request.Headers.Add("Origin", origin);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private async Task SeedUserAsync(long telegramUserId, MarketplaceRole role)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var user = PlatformUser.Create(telegramUserId, "Browser", null);
        user.SelectMarketplaceRole(role);
        dbContext.Add(user);
        await dbContext.SaveChangesAsync();
    }

    // The owner joins the team automatically when the company is saved (stage A).
    private async Task<Guid> SeedCompanyAsync(long owner, string name)
    {
        await SeedUserAsync(owner, MarketplaceRole.Business);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var company = BusinessProfile.Create(owner, name, "tashkent");
        company.Approve(verified: false);
        dbContext.Add(company);
        await dbContext.SaveChangesAsync();
        return company.Id;
    }

    private async Task SeedMemberAsync(Guid companyId, long telegramUserId, BusinessMemberRole role, long invitedBy)
    {
        await SeedUserAsync(telegramUserId, MarketplaceRole.Business);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        dbContext.BusinessMembers.Add(BusinessMember.Join(companyId, telegramUserId, role, invitedBy, DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private async Task RemoveMemberAsync(Guid companyId, long telegramUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var member = await dbContext.BusinessMembers.SingleAsync(candidate => candidate.BusinessId == companyId && candidate.TelegramUserId == telegramUserId && candidate.RemovedAtUtc == null);
        member.Remove(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> AuditActionsAsync(long actorTelegramUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        return await dbContext.AuditLogs.AsNoTracking().Where(entry => entry.ActorTelegramUserId == actorTelegramUserId).Select(entry => entry.Action).ToListAsync();
    }
}
