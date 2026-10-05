using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

public sealed class DealLifecycleIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    [IntegrationFact]
    public async Task Concurrent_complete_requests_persist_one_completion_timestamp()
    {
        var seed = await SeedCampaignDealAsync(1_100_001, 1_100_002);
        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);

        var responses = await Task.WhenAll(
            bloggerClient.PostAsync($"/api/deals/{seed.DealId}/complete", null),
            businessClient.PostAsync($"/api/deals/{seed.DealId}/complete", null),
            bloggerClient.PostAsync($"/api/deals/{seed.DealId}/complete", null));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var completedAt = new List<string?>();
        foreach (var response in responses)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal((int)DealStatus.Completed, body.GetProperty("status").GetInt32());
            completedAt.Add(body.GetProperty("completedAtUtc").GetString());
        }

        Assert.Single(completedAt.Distinct());
        var persisted = await FindDealAsync(seed.DealId);
        Assert.Equal(DealStatus.Completed, persisted.Status);
        Assert.NotNull(persisted.CompletedAtUtc);
    }

    [IntegrationFact]
    public async Task Repeated_complete_returns_the_same_completed_deal()
    {
        var seed = await SeedCampaignDealAsync(1_100_011, 1_100_012);
        using var client = CreateClient(seed.BloggerTelegramUserId);

        var first = await client.PostAsync($"/api/deals/{seed.DealId}/complete", null);
        var second = await client.PostAsync($"/api/deals/{seed.DealId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstBody.GetProperty("completedAtUtc").GetString(), secondBody.GetProperty("completedAtUtc").GetString());
    }

    [IntegrationFact]
    public async Task Concurrent_duplicate_reviews_return_conflict_instead_of_server_error()
    {
        var seed = await SeedCampaignDealAsync(1_100_021, 1_100_022, complete: true);
        using var client = CreateClient(seed.BloggerTelegramUserId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 5, comment = "Great" })));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(response => response.StatusCode != HttpStatusCode.Created),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        Assert.Equal(1, await dbContext.Reviews.CountAsync(review => review.DealId == seed.DealId));
    }

    [IntegrationFact]
    public async Task Deal_details_use_the_selected_role_and_hide_foreign_deals()
    {
        var seed = await SeedCampaignDealAsync(1_100_031, 1_100_032);
        var outsider = await SeedCampaignDealAsync(1_100_033, 1_100_034);
        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);

        var own = await bloggerClient.GetAsync($"/api/deals/me/{seed.DealId}");
        var foreign = await bloggerClient.GetAsync($"/api/deals/me/{outsider.DealId}");
        var missing = await bloggerClient.GetAsync($"/api/deals/me/{Guid.NewGuid()}");
        var foreignComplete = await bloggerClient.PostAsync($"/api/deals/{outsider.DealId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignComplete.StatusCode);
        Assert.Equal(DealStatus.Active, (await FindDealAsync(outsider.DealId)).Status);

        await SelectRoleAsync(seed.BloggerTelegramUserId, MarketplaceRole.BrandFace);
        var asBrandFace = await bloggerClient.GetAsync($"/api/deals/me/{seed.DealId}");
        var listAsBrandFace = await bloggerClient.GetFromJsonAsync<JsonElement>("/api/deals/me");

        Assert.Equal(HttpStatusCode.NotFound, asBrandFace.StatusCode);
        Assert.Equal(0, listAsBrandFace.GetArrayLength());
    }

    [IntegrationFact]
    public async Task Deal_details_keep_snapshot_terms_after_campaign_edit()
    {
        var seed = await SeedCampaignDealAsync(1_100_041, 1_100_042);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var campaign = await dbContext.Campaigns.SingleAsync(candidate => candidate.Id == seed.CampaignId);
            campaign.Update("Edited title", "Edited description", ["food"], null, 1, 2, "samarkand", null);
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateClient(seed.BusinessTelegramUserId);
        var details = await client.GetFromJsonAsync<JsonElement>($"/api/deals/me/{seed.DealId}");
        var list = await client.GetFromJsonAsync<JsonElement>("/api/deals/me");

        Assert.Equal("campaignApplication", details.GetProperty("sourceType").GetString());
        Assert.Equal("snapshot", details.GetProperty("termsSource").GetString());
        Assert.Equal("Original title", details.GetProperty("terms").GetProperty("title").GetString());
        Assert.Equal("Integration blogger", details.GetProperty("counterpartyName").GetString());
        var listed = Assert.Single(list.EnumerateArray());
        Assert.Equal("Original title", listed.GetProperty("title").GetString());
    }

    private async Task<SeededDeal> SeedCampaignDealAsync(long bloggerTelegramUserId, long businessTelegramUserId, bool complete = false)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var bloggerUser = PlatformUser.Create(bloggerTelegramUserId, "Blogger", null);
        bloggerUser.SelectMarketplaceRole(MarketplaceRole.Blogger);
        var businessUser = PlatformUser.Create(businessTelegramUserId, "Business", null);
        businessUser.SelectMarketplaceRole(MarketplaceRole.Business);
        var blogger = BloggerProfile.Create(bloggerTelegramUserId, "Integration blogger", "tashkent", ["beauty"]);
        blogger.Approve();
        var business = BusinessProfile.Create(businessTelegramUserId, "Integration business", "tashkent");
        business.Approve();
        var campaign = Campaign.Create(business.Id, "Original title", "Original description", ["beauty"], ["One reel"], 100, 200, "tashkent", null);
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        application.Accept();
        var deal = Deal.Create(application.Id, blogger.Id, business.Id, CampaignTermsSnapshot.FromCampaign(campaign));
        if (complete)
        {
            deal.Complete();
        }

        dbContext.AddRange(bloggerUser, businessUser, blogger, business, campaign, application, deal);
        await dbContext.SaveChangesAsync();
        return new SeededDeal(deal.Id, campaign.Id, bloggerTelegramUserId, businessTelegramUserId);
    }

    private async Task SelectRoleAsync(long telegramUserId, MarketplaceRole role)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var user = await dbContext.PlatformUsers.SingleAsync(candidate => candidate.TelegramUserId == telegramUserId);
        user.SelectMarketplaceRole(role);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Deal> FindDealAsync(Guid dealId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        return await dbContext.Deals.AsNoTracking().SingleAsync(deal => deal.Id == dealId);
    }

    private HttpClient CreateClient(long telegramUserId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("tma", CreateInitData(telegramUserId));
        return client;
    }

    private static string CreateInitData(long telegramUserId)
    {
        var authDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var user = JsonSerializer.Serialize(new { id = telegramUserId, first_name = "Integration", username = $"integration{telegramUserId}" });
        var dataCheckString = $"auth_date={authDate}\nuser={user}";
        using var secretKey = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secret = secretKey.ComputeHash(Encoding.UTF8.GetBytes(BloggerBazarApiFactory.BotToken));
        using var signature = new HMACSHA256(secret);
        var hash = Convert.ToHexString(signature.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
        return $"auth_date={authDate}&user={Uri.EscapeDataString(user)}&hash={hash}";
    }

    private sealed record SeededDeal(Guid DealId, Guid CampaignId, long BloggerTelegramUserId, long BusinessTelegramUserId);
}
