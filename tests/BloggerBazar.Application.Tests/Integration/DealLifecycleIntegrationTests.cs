using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

[Collection(ApiHostCollection.Name)]
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
        Assert.Equal(1, await dbContext.Reviews.IgnoreQueryFilters().CountAsync(review => review.DealId == seed.DealId));
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

    [IntegrationFact]
    public async Task Accepted_applications_expose_their_deal_to_both_participants()
    {
        var seed = await SeedCampaignDealAsync(1_100_051, 1_100_052);
        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);

        var details = await bloggerClient.GetFromJsonAsync<JsonElement>($"/api/campaign-applications/mine/{seed.ApplicationId}");
        var mine = await bloggerClient.GetFromJsonAsync<JsonElement>("/api/campaign-applications/mine");
        var inbox = await businessClient.GetFromJsonAsync<JsonElement>($"/api/campaigns/mine/{seed.CampaignId}/applications");

        Assert.Equal(seed.DealId, details.GetProperty("dealId").GetGuid());
        Assert.Equal(seed.DealId, Assert.Single(mine.GetProperty("items").EnumerateArray()).GetProperty("dealId").GetGuid());
        Assert.Equal(seed.DealId, Assert.Single(inbox.GetProperty("items").EnumerateArray()).GetProperty("dealId").GetGuid());
    }

    [IntegrationFact]
    public async Task Personal_contacts_are_visible_only_to_deal_participants()
    {
        var seed = await SeedCampaignDealAsync(1_100_061, 1_100_062);
        var stranger = await SeedCampaignDealAsync(1_100_063, 1_100_064);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);
        using var strangerClient = CreateClient(stranger.BusinessTelegramUserId);

        var forParticipant = await businessClient.GetAsync($"/api/contacts/Blogger/{seed.BloggerId}");
        var forStranger = await strangerClient.GetAsync($"/api/contacts/Blogger/{seed.BloggerId}");
        var dealContact = await businessClient.GetAsync($"/api/deals/me/{seed.DealId}/contact");
        var foreignDealContact = await strangerClient.GetAsync($"/api/deals/me/{seed.DealId}/contact");

        Assert.Equal(HttpStatusCode.OK, forParticipant.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, forStranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dealContact.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignDealContact.StatusCode);
    }

    [IntegrationFact]
    public async Task Offer_flow_creates_a_deal_only_after_the_blogger_accepts()
    {
        var (bloggerId, bloggerTelegramUserId, businessTelegramUserId) = await SeedParticipantsAsync(1_100_071, 1_100_072);
        using var bloggerClient = CreateClient(bloggerTelegramUserId);
        using var businessClient = CreateClient(businessTelegramUserId);
        var payload = new { bloggerId, format = "reels", offeredBudget = 1_500_000, message = "One reel about our launch" };

        var created = await businessClient.PostAsJsonAsync("/api/offers", payload);
        var duplicate = await businessClient.PostAsJsonAsync("/api/offers", payload);
        var contactBefore = await businessClient.GetAsync($"/api/contacts/Blogger/{bloggerId}");
        var incoming = await bloggerClient.GetFromJsonAsync<JsonElement>("/api/offers/mine");
        var offer = Assert.Single(incoming.EnumerateArray());
        var offerId = offer.GetProperty("id").GetGuid();
        var acceptedByBusiness = await businessClient.PostAsync($"/api/offers/mine/{offerId}/accept", null);
        var accepted = await bloggerClient.PostAsync($"/api/offers/mine/{offerId}/accept", null);
        var decision = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        var dealId = decision.GetProperty("dealId").GetGuid();
        var deal = await businessClient.GetFromJsonAsync<JsonElement>($"/api/deals/me/{dealId}");
        var contactAfter = await businessClient.GetAsync($"/api/contacts/Blogger/{bloggerId}");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, contactBefore.StatusCode);
        Assert.True(offer.GetProperty("canRespond").GetBoolean());
        Assert.Equal("pending", offer.GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.NotFound, acceptedByBusiness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("accepted", decision.GetProperty("state").GetString());
        Assert.Equal("collaborationRequest", deal.GetProperty("sourceType").GetString());
        Assert.Equal("reels", deal.GetProperty("offer").GetProperty("format").GetString());
        Assert.Equal(HttpStatusCode.OK, contactAfter.StatusCode);
    }

    [IntegrationFact]
    public async Task Partner_account_deletion_keeps_the_deal_and_reviews_without_the_partner_identity()
    {
        var seed = await SeedCampaignDealAsync(1_100_201, 1_100_202, complete: true);
        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await bloggerClient.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 4, comment = "Clear brief" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await businessClient.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 5, comment = "Great reel" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await businessClient.DeleteAsync("/api/users/me")).StatusCode);

        var deals = await bloggerClient.GetFromJsonAsync<JsonElement>("/api/deals/me");
        var deal = await bloggerClient.GetFromJsonAsync<JsonElement>($"/api/deals/me/{seed.DealId}");
        var contact = await bloggerClient.GetAsync($"/api/deals/me/{seed.DealId}/contact");
        var bloggerReviews = await anonymous.GetFromJsonAsync<JsonElement>($"/api/bloggers/{seed.BloggerId}/reviews");

        var listed = Assert.Single(deals.EnumerateArray(), item => item.GetProperty("id").GetGuid() == seed.DealId);
        Assert.True(listed.GetProperty("counterpartyDeleted").GetBoolean());
        Assert.Equal(string.Empty, listed.GetProperty("counterpartyName").GetString());
        Assert.True(deal.GetProperty("counterpartyDeleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, deal.GetProperty("counterpartyProfileId").ValueKind);
        Assert.False(deal.GetProperty("canComplete").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, contact.StatusCode);
        var review = Assert.Single(bloggerReviews.EnumerateArray());
        Assert.Equal("Great reel", review.GetProperty("comment").GetString());
        Assert.True(review.GetProperty("reviewerDeleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, review.GetProperty("reviewerName").ValueKind);
        Assert.Equal(JsonValueKind.Null, review.GetProperty("reviewerProfileId").ValueKind);
    }

    [IntegrationFact]
    public async Task Blind_reviews_stay_hidden_until_both_sides_review()
    {
        var seed = await SeedCampaignDealAsync(1_100_081, 1_100_082, complete: true);
        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);
        using var anonymous = factory.CreateClient();

        var first = await bloggerClient.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 4, comment = "Clear brief" });
        var hiddenBusinessReviews = await anonymous.GetFromJsonAsync<JsonElement>($"/api/businesses/{seed.BusinessId}/reviews");
        var bloggerDeal = await bloggerClient.GetFromJsonAsync<JsonElement>($"/api/deals/me/{seed.DealId}");
        var second = await businessClient.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 5, comment = "Great reel" });
        var businessReviews = await anonymous.GetFromJsonAsync<JsonElement>($"/api/businesses/{seed.BusinessId}/reviews");
        var bloggerReviews = await anonymous.GetFromJsonAsync<JsonElement>($"/api/bloggers/{seed.BloggerId}/reviews");
        var bloggerProfile = await anonymous.GetFromJsonAsync<JsonElement>($"/api/bloggers/{seed.BloggerId}");
        var businessReviewsNextPage = await anonymous.GetFromJsonAsync<JsonElement>($"/api/businesses/{seed.BusinessId}/reviews?skip=1");
        var bloggerReviewsNextPage = await anonymous.GetFromJsonAsync<JsonElement>($"/api/bloggers/{seed.BloggerId}/reviews?skip=1");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(0, hiddenBusinessReviews.GetProperty("reviewsCount").GetInt32());
        Assert.Empty(hiddenBusinessReviews.GetProperty("items").EnumerateArray());
        Assert.True(bloggerDeal.GetProperty("hasReviewed").GetBoolean());
        Assert.False(bloggerDeal.GetProperty("canReview").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, bloggerDeal.GetProperty("reviewDeadlineUtc").ValueKind);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(1, businessReviews.GetProperty("reviewsCount").GetInt32());
        Assert.Equal(4m, businessReviews.GetProperty("rating").GetDecimal());
        Assert.Equal("Integration blogger", Assert.Single(businessReviews.GetProperty("items").EnumerateArray()).GetProperty("reviewerName").GetString());
        var bloggerReview = Assert.Single(bloggerReviews.EnumerateArray());
        Assert.Equal("Great reel", bloggerReview.GetProperty("comment").GetString());
        Assert.Equal("Integration business", bloggerReview.GetProperty("reviewerName").GetString());
        Assert.Equal(1, bloggerProfile.GetProperty("reviewsCount").GetInt32());
        // A later page keeps the totals and holds only what is left.
        Assert.Equal(1, businessReviewsNextPage.GetProperty("reviewsCount").GetInt32());
        Assert.Empty(businessReviewsNextPage.GetProperty("items").EnumerateArray());
        Assert.Empty(bloggerReviewsNextPage.EnumerateArray());
    }

    [IntegrationFact]
    public async Task Lone_review_is_published_when_the_window_ends_and_reviewing_closes()
    {
        var seed = await SeedCampaignDealAsync(1_100_091, 1_100_092, complete: true);
        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await bloggerClient.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 3 })).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            await dbContext.Deals.Where(deal => deal.Id == seed.DealId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(deal => deal.CompletedAtUtc, (DateTime?)DateTime.UtcNow.AddDays(-15)));
            var published = await scope.ServiceProvider.GetRequiredService<IReviewRepository>().PublishRevealedAsync(null, DateTime.UtcNow, CancellationToken.None);
            Assert.True(published >= 1);
        }

        var businessReviews = await anonymous.GetFromJsonAsync<JsonElement>($"/api/businesses/{seed.BusinessId}/reviews");
        var businessDeal = await businessClient.GetFromJsonAsync<JsonElement>($"/api/deals/me/{seed.DealId}");
        var late = await businessClient.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 5 });

        Assert.Equal(1, businessReviews.GetProperty("reviewsCount").GetInt32());
        Assert.False(businessDeal.GetProperty("canReview").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    [IntegrationFact]
    public async Task Reminder_candidates_and_claims_are_idempotent()
    {
        var stuck = await SeedCampaignDealAsync(1_100_101, 1_100_102);
        var fresh = await SeedCampaignDealAsync(1_100_103, 1_100_104);
        var reviewed = await SeedCampaignDealAsync(1_100_105, 1_100_106, complete: true);
        var nowUtc = DateTime.UtcNow;
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        await dbContext.Deals.Where(deal => deal.Id == stuck.DealId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(deal => deal.CreatedAtUtc, nowUtc.AddDays(-8)));
        await dbContext.Deals.Where(deal => deal.Id == reviewed.DealId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(deal => deal.CompletedAtUtc, (DateTime?)nowUtc.AddDays(-2)));
        using (var bloggerClient = CreateClient(reviewed.BloggerTelegramUserId))
        {
            Assert.Equal(HttpStatusCode.Created, (await bloggerClient.PostAsJsonAsync($"/api/deals/{reviewed.DealId}/reviews", new { rating = 5 })).StatusCode);
        }

        var reminders = scope.ServiceProvider.GetRequiredService<IDealReminderRepository>();
        var candidates = await reminders.GetCandidatesAsync(nowUtc, CancellationToken.None);
        var firstClaim = await reminders.TryClaimAsync(stuck.DealId, DealReminderKind.CompleteDay7, MarketplaceRole.Blogger, nowUtc, CancellationToken.None);
        var secondClaim = await reminders.TryClaimAsync(stuck.DealId, DealReminderKind.CompleteDay7, MarketplaceRole.Blogger, nowUtc, CancellationToken.None);
        var otherSide = await reminders.TryClaimAsync(stuck.DealId, DealReminderKind.CompleteDay7, MarketplaceRole.Business, nowUtc, CancellationToken.None);

        Assert.Contains(candidates, candidate => candidate.DealId == stuck.DealId && candidate.Status == DealStatus.Active);
        Assert.DoesNotContain(candidates, candidate => candidate.DealId == fresh.DealId);
        var reviewedCandidate = Assert.Single(candidates, candidate => candidate.DealId == reviewed.DealId);
        Assert.True(reviewedCandidate.BloggerHasReviewed);
        Assert.False(reviewedCandidate.BusinessHasReviewed);
        Assert.Equal(reviewed.BusinessTelegramUserId, Assert.Single(DealReminderSchedule.Due(reviewedCandidate, nowUtc)).ChatId);
        Assert.True(firstClaim);
        Assert.False(secondClaim);
        Assert.True(otherSide);
    }

    private async Task<(Guid BloggerId, long BloggerTelegramUserId, long BusinessTelegramUserId)> SeedParticipantsAsync(long bloggerTelegramUserId, long businessTelegramUserId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var bloggerUser = PlatformUser.Create(bloggerTelegramUserId, "Blogger", null);
        bloggerUser.SelectMarketplaceRole(MarketplaceRole.Blogger);
        var businessUser = PlatformUser.Create(businessTelegramUserId, "Business", null);
        businessUser.SelectMarketplaceRole(MarketplaceRole.Business);
        var blogger = BloggerProfile.Create(bloggerTelegramUserId, "Offer blogger", "tashkent", ["beauty"]);
        blogger.Approve();
        var business = BusinessProfile.Create(businessTelegramUserId, "Offer business", "tashkent");
        business.Approve();
        dbContext.AddRange(bloggerUser, businessUser, blogger, business);
        await dbContext.SaveChangesAsync();
        return (blogger.Id, bloggerTelegramUserId, businessTelegramUserId);
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
        return new SeededDeal(deal.Id, campaign.Id, application.Id, blogger.Id, bloggerTelegramUserId, businessTelegramUserId, business.Id);
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

    private sealed record SeededDeal(Guid DealId, Guid CampaignId, Guid ApplicationId, Guid BloggerId, long BloggerTelegramUserId, long BusinessTelegramUserId, Guid BusinessId);
}
