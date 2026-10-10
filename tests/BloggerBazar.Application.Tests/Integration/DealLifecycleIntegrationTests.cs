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

    // D46: a brand face goes through the same cycle as a blogger: apply, accept, deal, complete, reviews.
    [IntegrationFact]
    public async Task Brand_face_applies_gets_a_deal_and_reviews_like_a_blogger()
    {
        const long brandFaceTelegramUserId = 1_100_301;
        const long businessTelegramUserId = 1_100_302;
        Guid brandFaceId, businessId, campaignId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var brandFaceUser = PlatformUser.Create(brandFaceTelegramUserId, "Brand face", null);
            brandFaceUser.SelectMarketplaceRole(MarketplaceRole.BrandFace);
            var businessUser = PlatformUser.Create(businessTelegramUserId, "Business", null);
            businessUser.SelectMarketplaceRole(MarketplaceRole.Business);
            var brandFace = BrandFaceProfile.Create(brandFaceTelegramUserId, "Integration brand face", "tashkent", ["beauty"]);
            var business = BusinessProfile.Create(businessTelegramUserId, "Brand face business", "tashkent");
            business.Approve();
            var campaign = Campaign.Create(business.Id, "Brand face campaign", "Description", ["beauty"], ["One reel"], 100, 200, "tashkent", null);
            campaign.Publish();
            dbContext.AddRange(brandFaceUser, businessUser, brandFace, business, campaign);
            await dbContext.SaveChangesAsync();
            (brandFaceId, businessId, campaignId) = (brandFace.Id, business.Id, campaign.Id);
        }

        using var brandFaceClient = CreateClient(brandFaceTelegramUserId);
        using var businessClient = CreateClient(businessTelegramUserId);
        using var anonymous = factory.CreateClient();

        var applied = await brandFaceClient.PostAsJsonAsync($"/api/campaigns/{campaignId}/applications", new { message = "Hello" });
        var duplicate = await brandFaceClient.PostAsJsonAsync($"/api/campaigns/{campaignId}/applications", new { message = "Again" });
        Assert.Equal(HttpStatusCode.Created, applied.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var applicationId = (await applied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var mine = await brandFaceClient.GetFromJsonAsync<JsonElement>("/api/campaign-applications/mine");
        Assert.Equal(applicationId, Assert.Single(mine.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        var inbox = await businessClient.GetFromJsonAsync<JsonElement>($"/api/campaigns/mine/{campaignId}/applications");
        var inboxItem = Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Equal("brandFace", inboxItem.GetProperty("creatorRole").GetString());
        Assert.Equal(brandFaceId, inboxItem.GetProperty("brandFaceId").GetGuid());
        Assert.Equal("Integration brand face", inboxItem.GetProperty("bloggerName").GetString());
        var campaignView = await brandFaceClient.GetFromJsonAsync<JsonElement>($"/api/campaigns/{campaignId}");
        Assert.Equal(1, campaignView.GetProperty("applicationsCount").GetInt32());

        var accepted = await businessClient.PostAsync($"/api/campaigns/mine/{campaignId}/applications/{applicationId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var dealId = (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dealId").GetGuid();

        var deals = await brandFaceClient.GetFromJsonAsync<JsonElement>("/api/deals/me");
        Assert.Equal(dealId, Assert.Single(deals.EnumerateArray()).GetProperty("id").GetGuid());
        var businessDeal = await businessClient.GetFromJsonAsync<JsonElement>($"/api/deals/me/{dealId}");
        Assert.Equal("Integration brand face", businessDeal.GetProperty("counterpartyName").GetString());
        Assert.Equal("brandFace", businessDeal.GetProperty("counterpartyRole").GetString());
        Assert.Equal(brandFaceId, businessDeal.GetProperty("counterpartyProfileId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await businessClient.GetAsync($"/api/deals/me/{dealId}/contact")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await brandFaceClient.GetAsync($"/api/deals/me/{dealId}/contact")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await brandFaceClient.PostAsync($"/api/deals/{dealId}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await brandFaceClient.PostAsJsonAsync($"/api/deals/{dealId}/reviews", new { rating = 4, comment = "Clear brief" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await businessClient.PostAsJsonAsync($"/api/deals/{dealId}/reviews", new { rating = 5, comment = "Great face" })).StatusCode);

        var brandFaceReviews = await anonymous.GetFromJsonAsync<JsonElement>($"/api/brand-faces/{brandFaceId}/reviews");
        Assert.Equal(1, brandFaceReviews.GetProperty("reviewsCount").GetInt32());
        Assert.Equal(5m, brandFaceReviews.GetProperty("rating").GetDecimal());
        var aboutBrandFace = Assert.Single(brandFaceReviews.GetProperty("items").EnumerateArray());
        Assert.Equal("Brand face business", aboutBrandFace.GetProperty("reviewerName").GetString());
        Assert.Equal("business", aboutBrandFace.GetProperty("reviewerRole").GetString());
        var businessReviews = await anonymous.GetFromJsonAsync<JsonElement>($"/api/businesses/{businessId}/reviews");
        var aboutBusiness = Assert.Single(businessReviews.GetProperty("items").EnumerateArray());
        Assert.Equal("Integration brand face", aboutBusiness.GetProperty("reviewerName").GetString());
        Assert.Equal("brandFace", aboutBusiness.GetProperty("reviewerRole").GetString());
        Assert.Equal(brandFaceId, aboutBusiness.GetProperty("reviewerProfileId").GetGuid());
    }

    // D48: a business offers a brand face a photo shoot; the brand face accepts and both see the deal.
    [IntegrationFact]
    public async Task Business_offer_to_a_brand_face_opens_a_deal_after_acceptance()
    {
        const long brandFaceTelegramUserId = 1_100_321;
        const long businessTelegramUserId = 1_100_322;
        Guid brandFaceId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var brandFaceUser = PlatformUser.Create(brandFaceTelegramUserId, "Brand face", null);
            brandFaceUser.SelectMarketplaceRole(MarketplaceRole.BrandFace);
            var businessUser = PlatformUser.Create(businessTelegramUserId, "Business", null);
            businessUser.SelectMarketplaceRole(MarketplaceRole.Business);
            var brandFace = BrandFaceProfile.Create(brandFaceTelegramUserId, "Offer brand face", "tashkent", ["beauty"]);
            var business = BusinessProfile.Create(businessTelegramUserId, "Offer business for face", "tashkent");
            business.Approve();
            dbContext.AddRange(brandFaceUser, businessUser, brandFace, business);
            await dbContext.SaveChangesAsync();
            brandFaceId = brandFace.Id;
        }

        using var businessClient = CreateClient(businessTelegramUserId);
        using var brandFaceClient = CreateClient(brandFaceTelegramUserId);

        var wrongFormat = await businessClient.PostAsJsonAsync("/api/offers", new { brandFaceId, format = "reels", message = "Reel" });
        var sent = await businessClient.PostAsJsonAsync("/api/offers", new { brandFaceId, format = "photoShoot", offeredBudget = 500000, message = "Spring shoot" });
        var duplicate = await businessClient.PostAsJsonAsync("/api/offers", new { brandFaceId, format = "video", message = "Again" });
        Assert.Equal((HttpStatusCode)422, wrongFormat.StatusCode);
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var offerId = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var inbox = await brandFaceClient.GetFromJsonAsync<JsonElement>("/api/offers/mine");
        var received = Assert.Single(inbox.EnumerateArray());
        Assert.True(received.GetProperty("canRespond").GetBoolean());
        Assert.Equal("photoShoot", received.GetProperty("format").GetString());
        Assert.Equal("Offer business for face", received.GetProperty("counterpartyName").GetString());

        var accepted = await brandFaceClient.PostAsync($"/api/offers/mine/{offerId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var dealId = (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("dealId").GetGuid();
        var businessDeal = await businessClient.GetFromJsonAsync<JsonElement>($"/api/deals/me/{dealId}");
        Assert.Equal("brandFace", businessDeal.GetProperty("counterpartyRole").GetString());
        Assert.Equal(brandFaceId, businessDeal.GetProperty("counterpartyProfileId").GetGuid());
        Assert.Equal("photoShoot", businessDeal.GetProperty("offer").GetProperty("format").GetString());
    }

    // D50: a paused blogger leaves the catalog, keeps the page by link and receives no new offers; resuming brings it back.
    [IntegrationFact]
    public async Task Hidden_blogger_leaves_the_catalog_but_keeps_its_page_and_deals()
    {
        var seed = await SeedCampaignDealAsync(1_100_331, 1_100_332);
        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);

        var hide = await bloggerClient.PutAsJsonAsync("/api/users/me/roles/blogger/visibility", new { hidden = true });
        var notMine = await bloggerClient.PutAsJsonAsync("/api/users/me/roles/brand-face/visibility", new { hidden = true });
        Assert.Equal(HttpStatusCode.OK, hide.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notMine.StatusCode);

        var catalog = await businessClient.GetFromJsonAsync<JsonElement>("/api/bloggers?query=Integration%20blogger&pageSize=50");
        var page = await businessClient.GetFromJsonAsync<JsonElement>($"/api/bloggers/{seed.BloggerId}");
        var offer = await businessClient.PostAsJsonAsync("/api/offers", new { bloggerId = seed.BloggerId, format = "reels", message = "Reel" });
        var deal = await bloggerClient.GetAsync($"/api/deals/me/{seed.DealId}");
        var mine = await bloggerClient.GetFromJsonAsync<JsonElement>("/api/bloggers/me");

        Assert.DoesNotContain(catalog.GetProperty("bloggers").EnumerateArray(), item => item.GetProperty("id").GetGuid() == seed.BloggerId);
        Assert.True(page.GetProperty("isHidden").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, offer.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deal.StatusCode);
        Assert.True(mine.GetProperty("isHidden").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await bloggerClient.PutAsJsonAsync("/api/users/me/roles/blogger/visibility", new { hidden = false })).StatusCode);
        var back = await businessClient.GetFromJsonAsync<JsonElement>("/api/bloggers?query=Integration%20blogger&pageSize=50");
        Assert.Contains(back.GetProperty("bloggers").EnumerateArray(), item => item.GetProperty("id").GetGuid() == seed.BloggerId);
    }

    [IntegrationFact]
    public async Task Deal_results_price_and_publications_round_trip_with_role_checks()
    {
        var seed = await SeedCampaignDealAsync(1_100_341, 1_100_342);
        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);

        Assert.Equal(HttpStatusCode.Forbidden, (await bloggerClient.PutAsJsonAsync($"/api/deals/me/{seed.DealId}/price", new { price = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await businessClient.PutAsJsonAsync($"/api/deals/me/{seed.DealId}/price", new { price = 1_500_000 })).StatusCode);

        var added = await bloggerClient.PostAsJsonAsync($"/api/deals/me/{seed.DealId}/publications", new { url = "https://instagram.com/p/integration", views = 4000 });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var publicationId = (await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var duplicate = await bloggerClient.PostAsJsonAsync($"/api/deals/me/{seed.DealId}/publications", new { url = "https://instagram.com/p/integration" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await businessClient.PostAsJsonAsync($"/api/deals/me/{seed.DealId}/publications", new { url = "https://instagram.com/p/other" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bloggerClient.PostAsync($"/api/deals/me/{seed.DealId}/publications/{publicationId}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await businessClient.PostAsync($"/api/deals/me/{seed.DealId}/publications/{publicationId}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await bloggerClient.DeleteAsync($"/api/deals/me/{seed.DealId}/publications/{publicationId}")).StatusCode);

        var forBusiness = await businessClient.GetFromJsonAsync<JsonElement>($"/api/deals/me/{seed.DealId}");
        Assert.Equal(1_500_000, forBusiness.GetProperty("agreedPrice").GetInt32());
        Assert.True(forBusiness.GetProperty("canSetPrice").GetBoolean());
        var publication = Assert.Single(forBusiness.GetProperty("publications").EnumerateArray());
        Assert.Equal(4000, publication.GetProperty("views").GetInt32());
        Assert.True(publication.GetProperty("confirmed").GetBoolean());

        // A stranger's lookup of the same deal and publication reads as missing.
        var stranger = await SeedParticipantsAsync(1_100_343, 1_100_344);
        using var strangerClient = CreateClient(stranger.BusinessTelegramUserId);
        Assert.Equal(HttpStatusCode.NotFound, (await strangerClient.PostAsync($"/api/deals/me/{seed.DealId}/publications/{publicationId}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await strangerClient.PutAsJsonAsync($"/api/deals/me/{seed.DealId}/price", new { price = 1 })).StatusCode);
    }

    [IntegrationFact]
    public async Task Database_rejects_a_deal_without_exactly_one_creator()
    {
        var seed = await SeedCampaignDealAsync(1_100_311, 1_100_312);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var brandFace = BrandFaceProfile.Create(1_100_313, "Second creator", "tashkent", ["beauty"]);
        dbContext.Add(brandFace);
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE deals SET \"BrandFaceId\" = {brandFace.Id} WHERE \"Id\" = {seed.DealId}"));
        await Assert.ThrowsAnyAsync<Exception>(() => dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE deals SET \"BloggerId\" = NULL WHERE \"Id\" = {seed.DealId}"));
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
