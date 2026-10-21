using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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

// Web admin phase 3 on a real database: the deal list, moderation in the journal and "sign out on all devices".
[Collection(ApiHostCollection.Name)]
public sealed class AdminWebActionsIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    private const string CookieName = "__Host-bb_admin";

    [IntegrationFact]
    public async Task The_deal_list_shows_both_sides_and_filters_by_status()
    {
        await SeedUserAsync(1_500_001, PlatformRole.Admin);
        var dealId = await SeedDealAsync(1_500_002, 1_500_003, complete: true);
        using var client = CreateClient();
        var token = await SignInAsync(client, 1_500_001);

        var completed = await (await SendAsync(client, HttpMethod.Get, "/api/admin/deals?status=completed&take=200", token)).Content.ReadFromJsonAsync<JsonElement>();
        var deal = completed.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == dealId);
        var active = await (await SendAsync(client, HttpMethod.Get, "/api/admin/deals?status=active&take=200", token)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Phase 3 campaign", deal.GetProperty("title").GetString());
        Assert.False(deal.GetProperty("fromOffer").GetBoolean());
        Assert.Equal("Phase 3 business", deal.GetProperty("businessName").GetString());
        Assert.Equal(1_500_003, deal.GetProperty("businessTelegramUserId").GetInt64());
        Assert.Equal("blogger", deal.GetProperty("creatorRole").GetString());
        Assert.Equal("Phase 3 blogger", deal.GetProperty("creatorName").GetString());
        Assert.Equal(1_500_002, deal.GetProperty("creatorTelegramUserId").GetInt64());
        Assert.Equal((int)DealStatus.Completed, deal.GetProperty("status").GetInt32());
        Assert.DoesNotContain(active.EnumerateArray(), item => item.GetProperty("id").GetGuid() == dealId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, HttpMethod.Get, "/api/admin/deals?status=Done", token)).StatusCode);

        // Completed without a price: listed under "without price" until the business enters one, 0 included.
        Assert.Contains((await GetDealIdsAsync(client, "withoutPrice", token)), id => id == dealId);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>().Deals.Where(item => item.Id == dealId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AgreedPrice, (int?)0));
        }

        Assert.DoesNotContain((await GetDealIdsAsync(client, "withoutPrice", token)), id => id == dealId);
    }

    private async Task<Guid[]> GetDealIdsAsync(HttpClient client, string status, string token)
    {
        var deals = await (await SendAsync(client, HttpMethod.Get, $"/api/admin/deals?status={status}&take=200", token)).Content.ReadFromJsonAsync<JsonElement>();
        return deals.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
    }

    [IntegrationFact]
    public async Task A_complaint_review_shows_the_accused_record_the_reporter_and_their_shared_deal()
    {
        await SeedUserAsync(1_500_041, PlatformRole.Admin);
        var (complaintId, dealId) = await SeedComplaintCaseAsync(1_500_042, 1_500_043);
        using var client = CreateClient();
        var token = await SignInAsync(client, 1_500_041);

        var response = await SendAsync(client, HttpMethod.Get, $"/api/admin/complaints/{complaintId}/review", token);
        var review = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(complaintId, review.GetProperty("complaint").GetProperty("id").GetGuid());
        Assert.Equal(1_500_042, review.GetProperty("accusedTelegramUserId").GetInt64());
        Assert.Equal(1, review.GetProperty("accused").GetProperty("completedDeals").GetInt32());
        Assert.Equal(1, review.GetProperty("reviews").GetProperty("count").GetInt32());
        Assert.Equal(2, review.GetProperty("reviews").GetProperty("average").GetDouble());
        Assert.Equal(1, review.GetProperty("otherComplaints").GetArrayLength());
        Assert.Equal(1_500_043, review.GetProperty("reporter").GetProperty("telegramUserId").GetInt64());
        Assert.Equal(1, review.GetProperty("reporter").GetProperty("filed").GetInt32());
        var shared = Assert.Single(review.GetProperty("sharedHistory").EnumerateArray());
        Assert.Equal("deal", shared.GetProperty("kind").GetString());
        Assert.Equal(dealId, shared.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, $"/api/admin/complaints/{Guid.NewGuid()}/review", token)).StatusCode);
    }

    [IntegrationFact]
    public async Task The_journal_can_be_limited_to_tashkent_days()
    {
        await SeedUserAsync(1_500_051, PlatformRole.Admin);
        using var client = CreateClient();
        var token = await SignInAsync(client, 1_500_051);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

        var todays = await (await SendAsync(client, HttpMethod.Get, $"/api/admin/audit-logs?take=500&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}", token)).Content.ReadFromJsonAsync<JsonElement>();
        var past = await (await SendAsync(client, HttpMethod.Get, "/api/admin/audit-logs?take=500&from=2020-01-01&to=2020-01-31", token)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Contains(todays.EnumerateArray(), entry => entry.GetProperty("actorTelegramUserId").GetInt64() == 1_500_051 && entry.GetProperty("action").GetString() == AdminWebAuditActions.SignedIn);
        Assert.Equal(0, past.GetArrayLength());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, HttpMethod.Get, "/api/admin/audit-logs?from=2026-10-12&to=2026-10-10", token)).StatusCode);
    }

    [IntegrationFact]
    public async Task An_export_is_journaled_before_the_file_and_needs_the_admin_site_origin()
    {
        await SeedUserAsync(1_500_061, PlatformRole.Admin);
        using var client = CreateClient();
        var token = await SignInAsync(client, 1_500_061);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendJsonAsync(client, "/api/admin-web/exports", token, "https://evil.example", new { kind = "users", rows = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendJsonAsync(client, "/api/admin-web/exports", token, BloggerBazarApiFactory.AdminWebOrigin, new { kind = "secrets", rows = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendJsonAsync(client, "/api/admin-web/exports", token, BloggerBazarApiFactory.AdminWebOrigin, new { kind = "users", rows = 3 })).StatusCode);

        var entry = Assert.Single(await AuditEntriesAsync(1_500_061), item => item.Action == AdminWebAuditActions.Exported);
        Assert.Equal(("users", "3"), (entry.TargetId, entry.Details));
    }

    [IntegrationFact]
    public async Task Members_cannot_list_deals()
    {
        await SeedUserAsync(1_500_011, PlatformRole.Member);
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/deals");
        request.Headers.Add("Authorization", $"tma {CreateInitData(1_500_011)}");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
    }

    [IntegrationFact]
    public async Task Profile_moderation_is_written_to_the_journal()
    {
        await SeedUserAsync(1_500_021, PlatformRole.Admin);
        var (bloggerId, businessId) = await SeedPendingProfilesAsync(1_500_022, 1_500_023);
        using var client = CreateClient();
        var token = await SignInAsync(client, 1_500_021);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, $"/api/admin/bloggers/{bloggerId}/needs-changes", token, BloggerBazarApiFactory.AdminWebOrigin)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, $"/api/admin/businesses/{businessId}/approve", token, BloggerBazarApiFactory.AdminWebOrigin)).StatusCode);

        var entries = await AuditEntriesAsync(1_500_021);
        Assert.Contains(entries, entry => entry.Action == "blogger-profile.needs-changes" && entry.TargetType == "BloggerProfile" && entry.TargetId == bloggerId.ToString());
        Assert.Contains(entries, entry => entry.Action == "business-profile.approved" && entry.TargetType == "BusinessProfile" && entry.TargetId == businessId.ToString());
    }

    [IntegrationFact]
    public async Task Signing_out_everywhere_ends_every_browser_and_needs_the_admin_site_origin()
    {
        await SeedUserAsync(1_500_031, PlatformRole.Admin);
        using var client = CreateClient();
        var laptop = await SignInAsync(client, 1_500_031);
        var phone = await SignInAsync(client, 1_500_031, secondsAgo: 5);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Post, "/api/admin-web/logout-all", laptop, "https://evil.example")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/admin-web/session", phone)).StatusCode);

        var response = await SendAsync(client, HttpMethod.Post, "/api/admin-web/logout-all", laptop, BloggerBazarApiFactory.AdminWebOrigin);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.StartsWith($"{CookieName}=;", Assert.Single(response.Headers.GetValues("Set-Cookie")), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin-web/session", laptop)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin/dashboard", phone)).StatusCode);
        Assert.Contains(await AuditEntriesAsync(1_500_031), entry => entry.Action == AdminWebAuditActions.SignedOutEverywhere);

        // A sign-in after that works as usual.
        var later = await SignInAsync(client, 1_500_031, secondsAgo: 10);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/admin-web/session", later)).StatusCode);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    // Each sign-in needs its own signed data (the same data is accepted once), so the age differs per call.
    private static async Task<string> SignInAsync(HttpClient client, long telegramUserId, int secondsAgo = 0)
    {
        var signed = LoginWidgetTestData.Signed(BloggerBazarApiFactory.BotToken, telegramUserId, DateTimeOffset.UtcNow.AddSeconds(-secondsAgo));
        var response = await AdminWebApiTests.SignInAsync(client, signed, BloggerBazarApiFactory.AdminWebOrigin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        return setCookie[(CookieName.Length + 1)..].Split(';')[0];
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string token, string? origin = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", $"{CookieName}={token}");
        if (origin is not null) request.Headers.Add("Origin", origin);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, string path, string token, string origin, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Cookie", $"{CookieName}={token}");
        request.Headers.Add("Origin", origin);
        return await client.SendAsync(request);
    }

    private async Task SeedUserAsync(long telegramUserId, PlatformRole role)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var user = PlatformUser.Create(telegramUserId, "Phase 3", null);
        user.SetRole(role);
        dbContext.Add(user);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedDealAsync(long bloggerTelegramUserId, long businessTelegramUserId, bool complete)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var blogger = BloggerProfile.Create(bloggerTelegramUserId, "Phase 3 blogger", "tashkent", ["beauty"]);
        blogger.Approve();
        var business = BusinessProfile.Create(businessTelegramUserId, "Phase 3 business", "tashkent");
        business.Approve();
        var campaign = Campaign.Create(business.Id, "Phase 3 campaign", "Description", ["beauty"], ["One reel"], 100, 200, "tashkent", null);
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        application.Accept();
        var deal = Deal.Create(application.Id, blogger.Id, business.Id, CampaignTermsSnapshot.FromCampaign(campaign));
        if (complete) deal.Complete();
        dbContext.AddRange(PlatformUser.Create(bloggerTelegramUserId, "Blogger", null), PlatformUser.Create(businessTelegramUserId, "Business", null), blogger, business, campaign, application, deal);
        await dbContext.SaveChangesAsync();
        return deal.Id;
    }

    // A business (reporter) and a blogger (accused) with a completed deal, a 2-star review of the blogger, the case
    // itself and one earlier case about the same blogger from someone else.
    private async Task<(Guid ComplaintId, Guid DealId)> SeedComplaintCaseAsync(long bloggerTelegramUserId, long businessTelegramUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var blogger = BloggerProfile.Create(bloggerTelegramUserId, "Accused blogger", "tashkent", ["beauty"]);
        blogger.Approve();
        var business = BusinessProfile.Create(businessTelegramUserId, "Reporting business", "tashkent");
        business.Approve();
        var campaign = Campaign.Create(business.Id, "Review campaign", "Description", ["beauty"], null, 100, 200, "tashkent", null);
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        application.Accept();
        var deal = Deal.Create(application.Id, blogger.Id, business.Id, CampaignTermsSnapshot.FromCampaign(campaign));
        deal.Complete();
        var review = Review.ForBlogger(deal.Id, businessTelegramUserId, blogger.Id, 2, "Late");
        var complaint = Complaint.Create(businessTelegramUserId, MarketplaceRole.Business, "blogger", blogger.Id, "noShow", "Did not post", DateTime.UtcNow);
        var earlier = Complaint.Create(1_500_049, MarketplaceRole.Business, "blogger", blogger.Id, "spam", null, DateTime.UtcNow.AddDays(-3));
        dbContext.AddRange(PlatformUser.Create(bloggerTelegramUserId, "Accused", null), PlatformUser.Create(businessTelegramUserId, "Reporter", null),
            blogger, business, campaign, application, deal, review, complaint, earlier);
        await dbContext.SaveChangesAsync();
        return (complaint.Id, deal.Id);
    }

    private async Task<(Guid BloggerId, Guid BusinessId)> SeedPendingProfilesAsync(long bloggerTelegramUserId, long businessTelegramUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var blogger = BloggerProfile.Create(bloggerTelegramUserId, "Pending blogger", "tashkent", ["beauty"]);
        blogger.SubmitForModeration();
        var business = BusinessProfile.Create(businessTelegramUserId, "Pending business", "tashkent");
        dbContext.AddRange(blogger, business);
        await dbContext.SaveChangesAsync();
        return (blogger.Id, business.Id);
    }

    private async Task<IReadOnlyList<AuditLog>> AuditEntriesAsync(long actorTelegramUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        return await dbContext.AuditLogs.AsNoTracking().Where(entry => entry.ActorTelegramUserId == actorTelegramUserId).ToListAsync();
    }

    private static string CreateInitData(long telegramUserId)
    {
        var authDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var user = JsonSerializer.Serialize(new { id = telegramUserId, first_name = "Integration", username = $"integration{telegramUserId}" });
        var dataCheckString = $"auth_date={authDate}\nuser={user}";
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(BloggerBazarApiFactory.BotToken));
        var hash = Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
        return $"auth_date={authDate}&user={Uri.EscapeDataString(user)}&hash={hash}";
    }
}
