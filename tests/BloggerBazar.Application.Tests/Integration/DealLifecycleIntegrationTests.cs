using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Features.Tips;
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
        Assert.False(reviewedCandidate.HasPublications);
        // Two days after completion: the business still owes a review, and the blogger is asked for the publication link.
        Assert.Equal(
            [(reviewed.BusinessTelegramUserId, DealReminderKind.ReviewDay1), (reviewed.BloggerTelegramUserId, DealReminderKind.ResultsDay2)],
            DealReminderSchedule.Due(reviewedCandidate, nowUtc).Select(reminder => (reminder.ChatId, reminder.Kind)));
        Assert.True(firstClaim);
        Assert.False(secondClaim);
        Assert.True(otherSide);
    }

    [IntegrationFact]
    public async Task Reviewed_deal_without_publication_stays_a_candidate_until_the_results_reminder_is_claimed()
    {
        var seed = await SeedCampaignDealAsync(1_100_107, 1_100_108, complete: true);
        var nowUtc = DateTime.UtcNow;
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        await dbContext.Deals.Where(deal => deal.Id == seed.DealId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(deal => deal.CompletedAtUtc, (DateTime?)nowUtc.AddDays(-3)));
        using (var bloggerClient = CreateClient(seed.BloggerTelegramUserId))
        using (var businessClient = CreateClient(seed.BusinessTelegramUserId))
        {
            Assert.Equal(HttpStatusCode.Created, (await bloggerClient.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 5 })).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await businessClient.PostAsJsonAsync($"/api/deals/{seed.DealId}/reviews", new { rating = 5 })).StatusCode);
        }

        var reminders = scope.ServiceProvider.GetRequiredService<IDealReminderRepository>();
        var candidate = Assert.Single(await reminders.GetCandidatesAsync(nowUtc, CancellationToken.None), item => item.DealId == seed.DealId);
        var due = Assert.Single(DealReminderSchedule.Due(candidate, nowUtc));
        Assert.Equal((seed.BloggerTelegramUserId, DealReminderKind.ResultsDay2), (due.ChatId, due.Kind));

        Assert.True(await reminders.TryClaimAsync(seed.DealId, DealReminderKind.ResultsDay2, MarketplaceRole.Blogger, nowUtc, CancellationToken.None));
        Assert.DoesNotContain(await reminders.GetCandidatesAsync(nowUtc, CancellationToken.None), item => item.DealId == seed.DealId);
    }

    // Audit 2026-10-12: an accept and a withdrawal sent at the same moment. Exactly one wins; a deal exists only for
    // an accepted application (a withdrawn creator's contacts never open, D31).
    [IntegrationFact]
    public async Task Accept_and_withdraw_racing_never_leave_a_deal_behind_a_withdrawn_application()
    {
        for (var round = 0; round < 6; round++)
        {
            var bloggerTelegramUserId = 1_100_920 + round * 2;
            var seed = await SeedPendingApplicationAsync(bloggerTelegramUserId, bloggerTelegramUserId + 1);
            using var businessClient = CreateClient(bloggerTelegramUserId + 1);
            using var bloggerClient = CreateClient(bloggerTelegramUserId);

            var responses = await Task.WhenAll(
                businessClient.PostAsync($"/api/campaigns/mine/{seed.CampaignId}/applications/{seed.ApplicationId}/accept", null),
                bloggerClient.PostAsync($"/api/campaign-applications/mine/{seed.ApplicationId}/withdraw", null));

            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var status = await dbContext.CampaignApplications.AsNoTracking().Where(application => application.Id == seed.ApplicationId).Select(application => application.Status).SingleAsync();
            var hasDeal = await dbContext.Deals.AnyAsync(deal => deal.CampaignApplicationId == seed.ApplicationId);
            Assert.Contains(status, new[] { CampaignApplicationStatus.Accepted, CampaignApplicationStatus.Withdrawn });
            Assert.Equal(status == CampaignApplicationStatus.Accepted, hasDeal);
            Assert.Single(responses, response => response.IsSuccessStatusCode);
            Assert.All(responses.Where(response => !response.IsSuccessStatusCode), response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        }
    }

    [IntegrationFact]
    public async Task Pending_application_is_decided_once_at_the_database_level()
    {
        var seed = await SeedPendingApplicationAsync(1_100_934, 1_100_935);
        using var scope = factory.Services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<ICampaignApplicationRepository>();

        Assert.True(await applications.TryDecidePendingAsync(seed.ApplicationId, CampaignApplicationStatus.Withdrawn, CancellationToken.None));
        Assert.False(await applications.TryDecidePendingAsync(seed.ApplicationId, CampaignApplicationStatus.Accepted, CancellationToken.None));
        Assert.False(await applications.TryDecidePendingAsync(seed.ApplicationId, CampaignApplicationStatus.Rejected, CancellationToken.None));
    }

    // Audit 2026-10-12: the business could put a campaign the admin rejected back into the catalog (reopen, or close
    // then reopen). Now the rejection stays and is written to the journal.
    [IntegrationFact]
    public async Task Business_cannot_republish_a_campaign_the_admin_rejected()
    {
        const long adminTelegramUserId = 1_100_936;
        var seed = await SeedPendingApplicationAsync(1_100_937, 1_100_938);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var admin = PlatformUser.Create(adminTelegramUserId, "Admin", null);
            admin.SetRole(PlatformRole.Admin);
            dbContext.Add(admin);
            await dbContext.SaveChangesAsync();
        }

        using var adminClient = CreateClient(adminTelegramUserId);
        using var businessClient = CreateClient(1_100_938);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PatchAsJsonAsync($"/api/admin/campaigns/{seed.CampaignId}", new { status = (int)CampaignStatus.Rejected })).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await businessClient.PostAsync($"/api/campaigns/mine/{seed.CampaignId}/reopen", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await businessClient.PostAsync($"/api/campaigns/mine/{seed.CampaignId}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await businessClient.GetAsync($"/api/campaigns/{seed.CampaignId}")).StatusCode);
        var log = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/audit-logs");
        Assert.Contains(log.EnumerateArray(), entry => entry.GetProperty("action").GetString() == "campaign.status-changed"
            && entry.GetProperty("targetId").GetString() == seed.CampaignId.ToString());
    }

    [IntegrationFact]
    public async Task Owner_role_is_never_granted_through_the_admin_api()
    {
        const long ownerTelegramUserId = 1_100_939;
        const long memberTelegramUserId = 1_100_940;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var owner = PlatformUser.Create(ownerTelegramUserId, "Owner", null);
            owner.SetRole(PlatformRole.Owner);
            dbContext.AddRange(owner, PlatformUser.Create(memberTelegramUserId, "Member", null));
            await dbContext.SaveChangesAsync();
        }

        using var ownerClient = CreateClient(ownerTelegramUserId);
        var grantOwner = await ownerClient.PatchAsJsonAsync($"/api/admin/users/{memberTelegramUserId}/role", new { role = (int)PlatformRole.Owner });
        var grantAdmin = await ownerClient.PatchAsJsonAsync($"/api/admin/users/{memberTelegramUserId}/role", new { role = (int)PlatformRole.Admin });

        Assert.Equal(HttpStatusCode.Conflict, grantOwner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, grantAdmin.StatusCode);
    }

    // Audit 2026-10-12: legacy routes the app no longer calls are gone (direct requests bypassed offer limits and the
    // selected role), and the legacy accept answers 404 for another business's application, never 403.
    [IntegrationFact]
    public async Task Legacy_routes_are_gone_and_a_foreign_application_reads_as_missing()
    {
        var seed = await SeedPendingApplicationAsync(1_100_941, 1_100_942);
        var other = await SeedPendingApplicationAsync(1_100_943, 1_100_944);
        using var businessClient = CreateClient(1_100_942);

        Assert.Equal(HttpStatusCode.NotFound, (await businessClient.PostAsJsonAsync($"/api/collaboration-requests/bloggers/{seed.BloggerId}", new { message = "Hello" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await businessClient.GetAsync("/api/collaboration-requests/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await businessClient.PatchAsJsonAsync($"/api/campaign-applications/{seed.ApplicationId}/status", new { status = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await businessClient.PostAsync($"/api/campaign-applications/{other.ApplicationId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await businessClient.PostAsync($"/api/campaign-applications/{seed.ApplicationId}/accept", null)).StatusCode);
    }

    [IntegrationFact]
    public async Task Api_responses_carry_security_headers_including_errors()
    {
        using var client = CreateClient(1_100_903);
        using var ok = await client.GetAsync("/api/users/me");
        using var missing = await client.GetAsync($"/api/deals/me/{Guid.NewGuid()}");

        foreach (var response in new[] { ok, missing })
        {
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.False(missing.IsSuccessStatusCode);
    }

    [IntegrationFact]
    public async Task Bot_tips_can_be_switched_off_and_are_claimed_once_a_day()
    {
        const long tipsOn = 1_100_901;
        const long tipsOff = 1_100_902;
        using (var onClient = CreateClient(tipsOn))
        {
            Assert.True((await onClient.GetFromJsonAsync<JsonElement>("/api/users/me")).GetProperty("tipsEnabled").GetBoolean());
        }

        using (var offClient = CreateClient(tipsOff))
        {
            Assert.Equal(HttpStatusCode.OK, (await offClient.GetAsync("/api/users/me")).StatusCode);
            var off = await offClient.PutAsJsonAsync("/api/users/me/tips", new { enabled = false });
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);
            Assert.False((await off.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tipsEnabled").GetBoolean());
        }

        // Both joined just now, so the check runs as if the next day has started.
        var dayStartUtc = DateTime.UtcNow.AddMinutes(1);
        var claimAtUtc = dayStartUtc.AddMinutes(1);
        using var scope = factory.Services.CreateScope();
        var tips = scope.ServiceProvider.GetRequiredService<IBotTipRepository>();
        var due = await tips.GetDueAsync(dayStartUtc, 1000, CancellationToken.None);
        Assert.Contains(due, recipient => recipient.TelegramUserId == tipsOn);
        Assert.DoesNotContain(due, recipient => recipient.TelegramUserId == tipsOff);

        Assert.True(await tips.TryClaimAsync(tipsOn, 5, dayStartUtc, claimAtUtc, CancellationToken.None));
        Assert.False(await tips.TryClaimAsync(tipsOn, 6, dayStartUtc, claimAtUtc, CancellationToken.None));
        Assert.DoesNotContain(await tips.GetDueAsync(dayStartUtc, 1000, CancellationToken.None), recipient => recipient.TelegramUserId == tipsOn);
        var stored = await scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>().PlatformUsers.AsNoTracking().SingleAsync(user => user.TelegramUserId == tipsOn);
        Assert.Equal(5, stored.LastTipNumber);
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
            brandFace.Update("Offer brand face", "tashkent", null, null, [], ["beauty"], null, "@face.insta", "@face_tg", null, null, null, null);
            var business = BusinessProfile.Create(businessTelegramUserId, "Offer business for face", "tashkent");
            business.Approve();
            dbContext.AddRange(brandFaceUser, businessUser, brandFace, business);
            await dbContext.SaveChangesAsync();
            brandFaceId = brandFace.Id;
        }

        using var businessClient = CreateClient(businessTelegramUserId);
        using var brandFaceClient = CreateClient(brandFaceTelegramUserId);

        // D31: the public page keeps Instagram but not Telegram; the contact waits for a deal.
        var publicPage = await businessClient.GetFromJsonAsync<JsonElement>($"/api/brand-faces/{brandFaceId}");
        Assert.Equal("@face.insta", publicPage.GetProperty("instagram").GetString());
        Assert.False(publicPage.TryGetProperty("telegram", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await businessClient.GetAsync($"/api/contacts/BrandFace/{brandFaceId}")).StatusCode);

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
        var contact = await businessClient.GetFromJsonAsync<JsonElement>($"/api/contacts/BrandFace/{brandFaceId}");
        Assert.Equal("@face_tg", contact.GetProperty("telegram").GetString());
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
    public async Task Complaint_reaches_the_admin_queue_once_and_closes()
    {
        var seed = await SeedCampaignDealAsync(1_100_351, 1_100_352);
        const long adminTelegramUserId = 1_100_353;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var admin = PlatformUser.Create(adminTelegramUserId, "Admin", null);
            admin.SetRole(PlatformRole.Admin);
            dbContext.Add(admin);
            await dbContext.SaveChangesAsync();
        }

        using var businessClient = CreateClient(seed.BusinessTelegramUserId);
        using var adminClient = CreateClient(adminTelegramUserId);
        using var strangerClient = CreateClient(1_100_354);
        var report = new { targetType = "blogger", targetId = seed.BloggerId, reason = "fake", comment = "Followers look bought" };

        var first = await businessClient.PostAsJsonAsync("/api/complaints", report);
        var again = await businessClient.PostAsJsonAsync("/api/complaints", report);
        var foreignDeal = await strangerClient.PostAsJsonAsync("/api/complaints", new { targetType = "deal", targetId = seed.DealId, reason = "noShow" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True((await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alreadyReported").GetBoolean());
        Assert.NotEqual(HttpStatusCode.OK, foreignDeal.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await businessClient.GetAsync("/api/admin/complaints")).StatusCode);
        var queue = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/complaints");
        var item = Assert.Single(queue.EnumerateArray(), entry => entry.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
        Assert.Equal("Integration blogger", item.GetProperty("targetName").GetString());
        Assert.Equal(seed.BloggerTelegramUserId, item.GetProperty("ownerTelegramUserId").GetInt64());
        Assert.False(item.GetProperty("ownerIsBlocked").GetBoolean());
        Assert.False(item.GetProperty("ownerIsPlatformOwner").GetBoolean());
        var dashboard = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        Assert.True(dashboard.GetProperty("openComplaints").GetInt32() >= 1);
        // The Mini App admin links to the full admin site configured in AdminWeb:Origin.
        Assert.Equal(BloggerBazarApiFactory.AdminWebOrigin, dashboard.GetProperty("adminWebUrl").GetString());
        var businesses = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/businesses");
        Assert.Contains(businesses.EnumerateArray(), entry => entry.GetProperty("id").GetGuid() == seed.BusinessId);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/admin/bloggers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await businessClient.GetAsync("/api/admin/businesses")).StatusCode);
        var card = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/users/{seed.BloggerTelegramUserId}");
        Assert.Contains(card.GetProperty("profiles").EnumerateArray(), profile => profile.GetProperty("role").GetString() == "blogger" && profile.GetProperty("id").GetGuid() == seed.BloggerId);
        Assert.Contains(card.GetProperty("recentDeals").EnumerateArray(), deal => deal.GetProperty("id").GetGuid() == seed.DealId && deal.GetProperty("role").GetString() == "blogger");
        Assert.True(card.GetProperty("complaintsAgainst").GetInt32() >= 1);
        Assert.True(card.GetProperty("activeDeals").GetInt32() + card.GetProperty("completedDeals").GetInt32() >= 1);
        var businessCard = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/users/{seed.BusinessTelegramUserId}");
        Assert.Equal(1, businessCard.GetProperty("complaintsFiled").GetInt32());
        Assert.Contains(businessCard.GetProperty("recentDeals").EnumerateArray(), deal => deal.GetProperty("role").GetString() == "business" && deal.GetProperty("partnerName").GetString() == "Integration blogger");
        Assert.Equal(HttpStatusCode.Forbidden, (await businessClient.GetAsync($"/api/admin/users/{seed.BloggerTelegramUserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adminClient.GetAsync("/api/admin/users/999999999")).StatusCode);

        var close = await adminClient.PatchAsJsonAsync($"/api/admin/complaints/{item.GetProperty("id").GetGuid()}", new { status = "resolved", note = "Checked" });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PatchAsJsonAsync($"/api/admin/complaints/{item.GetProperty("id").GetGuid()}", new { status = "dismissed" })).StatusCode);
        var resolved = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/complaints?status=resolved");
        Assert.Contains(resolved.EnumerateArray(), entry => entry.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
    }

    [IntegrationFact]
    public async Task For_you_matches_by_category_and_city_and_skips_applied_campaigns()
    {
        var seed = await SeedCampaignDealAsync(1_100_361, 1_100_362);
        Guid matching, unrelated;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var match = Campaign.Create(seed.BusinessId, "For you match", "Description", ["beauty"], ["One reel"], 100, 200, "tashkent", null);
            match.Publish();
            var other = Campaign.Create(seed.BusinessId, "For you unrelated", "Description", ["cars"], ["One reel"], 100, 200, "bukhara", null);
            other.Publish();
            var applied = await dbContext.Campaigns.SingleAsync(campaign => campaign.Id == seed.CampaignId);
            applied.Publish();
            dbContext.AddRange(match, other);
            await dbContext.SaveChangesAsync();
            (matching, unrelated) = (match.Id, other.Id);
        }

        using var bloggerClient = CreateClient(seed.BloggerTelegramUserId);
        using var businessClient = CreateClient(seed.BusinessTelegramUserId);
        var forBlogger = await bloggerClient.GetFromJsonAsync<JsonElement>("/api/marketplace/for-me");
        var forBusiness = await businessClient.GetFromJsonAsync<JsonElement>("/api/marketplace/for-me");

        Assert.Equal("creator", forBlogger.GetProperty("audience").GetString());
        var campaignIds = forBlogger.GetProperty("campaigns").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(matching, campaignIds);
        Assert.DoesNotContain(unrelated, campaignIds);
        Assert.DoesNotContain(seed.CampaignId, campaignIds);
        Assert.Equal("business", forBusiness.GetProperty("audience").GetString());
        Assert.Contains(forBusiness.GetProperty("bloggers").EnumerateArray(), item => item.GetProperty("id").GetGuid() == seed.BloggerId);
    }

    [IntegrationFact]
    public async Task Admin_finds_users_by_phone_and_filters_by_role()
    {
        var seed = await SeedCampaignDealAsync(1_100_371, 1_100_372);
        const long adminTelegramUserId = 1_100_373;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var admin = PlatformUser.Create(adminTelegramUserId, "Admin search", null);
            admin.SetRole(PlatformRole.Admin);
            var blogger = await dbContext.PlatformUsers.SingleAsync(user => user.TelegramUserId == seed.BloggerTelegramUserId);
            blogger.VerifyPhone("+998901110371", DateTime.UtcNow);
            dbContext.Add(admin);
            await dbContext.SaveChangesAsync();
        }

        using var adminClient = CreateClient(adminTelegramUserId);
        var byPhone = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/users?search=90%20111%2003%2071");
        var found = Assert.Single(byPhone.EnumerateArray());
        Assert.Equal(seed.BloggerTelegramUserId, found.GetProperty("telegramUserId").GetInt64());
        Assert.Equal("+998901110371", found.GetProperty("verifiedPhone").GetString());
        Assert.True(found.GetProperty("hasBloggerProfile").GetBoolean());

        var bloggers = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/users?filter=bloggers&take=200");
        Assert.Contains(bloggers.EnumerateArray(), user => user.GetProperty("telegramUserId").GetInt64() == seed.BloggerTelegramUserId);
        Assert.DoesNotContain(bloggers.EnumerateArray(), user => user.GetProperty("telegramUserId").GetInt64() == seed.BusinessTelegramUserId);
        var admins = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/users?filter=admins&take=200");
        Assert.Contains(admins.EnumerateArray(), user => user.GetProperty("telegramUserId").GetInt64() == adminTelegramUserId);
        var newcomers = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/users?filter=new&take=200");
        Assert.Contains(newcomers.EnumerateArray(), user => user.GetProperty("telegramUserId").GetInt64() == seed.BloggerTelegramUserId);
        var dashboard = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        Assert.True(dashboard.GetProperty("newUsersWeek").GetInt32() >= 3);
    }

    [IntegrationFact]
    public async Task Admin_funnel_counts_people_once_per_step_and_only_within_the_period()
    {
        const long adminTelegramUserId = 1_100_960;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var admin = PlatformUser.Create(adminTelegramUserId, "Funnel admin", null);
            admin.SetRole(PlatformRole.Admin);
            dbContext.Add(admin);
            await dbContext.SaveChangesAsync();
        }

        using var adminClient = CreateClient(adminTelegramUserId);
        var allBefore = await GetFunnelAsync(adminClient, "all");
        var monthBefore = await GetFunnelAsync(adminClient, "30d");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            PlatformUser Person(long telegramUserId, MarketplaceRole? role)
            {
                var user = PlatformUser.Create(telegramUserId, "Funnel person", null);
                if (role is { } selected) user.SelectMarketplaceRole(selected);
                dbContext.Add(user);
                return user;
            }

            // Opened the app only; picked the blogger role and left before the profile.
            Person(1_100_961, null);
            Person(1_100_962, MarketplaceRole.Blogger);
            // A blogger profile without activity; an applicant with two pending applications; a blogger with a reviewed deal.
            Person(1_100_963, MarketplaceRole.Blogger);
            var idle = BloggerProfile.Create(1_100_963, "Funnel idle", "tashkent", ["beauty"]);
            Person(1_100_964, MarketplaceRole.Blogger);
            var applicant = BloggerProfile.Create(1_100_964, "Funnel applicant", "tashkent", ["beauty"]);
            Person(1_100_965, MarketplaceRole.Blogger);
            var doer = BloggerProfile.Create(1_100_965, "Funnel doer", "tashkent", ["beauty"]);
            // A business that published, got applications, finished the deal and reviewed; another one with only a draft.
            Person(1_100_966, MarketplaceRole.Business);
            var business = BusinessProfile.Create(1_100_966, "Funnel business", "tashkent");
            Person(1_100_967, MarketplaceRole.Business);
            var drafter = BusinessProfile.Create(1_100_967, "Funnel drafter", "tashkent");
            // A brand face with an offer from the business, and a blogger who first opened the app 40 days ago.
            Person(1_100_968, MarketplaceRole.BrandFace);
            var face = BrandFaceProfile.Create(1_100_968, "Funnel face", "tashkent", ["beauty"]);
            Person(1_100_969, MarketplaceRole.Blogger);
            var old = BloggerProfile.Create(1_100_969, "Funnel old", "tashkent", ["beauty"]);

            var campaign = Campaign.Create(business.Id, "Funnel campaign", "Funnel description", ["beauty"], ["One reel"], 100, 200, "tashkent", null);
            campaign.Publish();
            var secondCampaign = Campaign.Create(business.Id, "Second funnel campaign", "Funnel description", ["beauty"], ["One reel"], 100, 200, "tashkent", null);
            secondCampaign.Publish();
            var draft = Campaign.Create(drafter.Id, "Draft funnel campaign", "Funnel description", ["beauty"], ["One reel"], 100, 200, "tashkent", null);
            var accepted = CampaignApplication.Create(campaign.Id, doer.Id, null);
            accepted.Accept();
            var deal = Deal.Create(accepted.Id, doer.Id, business.Id, CampaignTermsSnapshot.FromCampaign(campaign));
            deal.Complete();
            dbContext.AddRange(idle, applicant, doer, business, drafter, face, old, campaign, secondCampaign, draft,
                CampaignApplication.Create(campaign.Id, applicant.Id, null), CampaignApplication.Create(secondCampaign.Id, applicant.Id, null), accepted, deal,
                Review.ForBusiness(deal.Id, 1_100_965, business.Id, 5, null), Review.ForBlogger(deal.Id, 1_100_966, doer.Id, 5, null),
                CollaborationRequest.CreateOfferForBrandFace(face.Id, business.Id, "Photo shoot", CollaborationFormat.PhotoShoot, 300, null));
            await dbContext.SaveChangesAsync();
            await dbContext.PlatformUsers.Where(user => user.TelegramUserId == 1_100_969)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.CreatedAtUtc, DateTime.UtcNow.AddDays(-40)));
        }

        var all = await GetFunnelAsync(adminClient, "all");
        Assert.Equal(["opened", "roleSelected", "profile", "reached", "deal", "completed", "reviewed"], all["blogger"].Select(step => step.Key));
        Assert.Equal(["opened", "roleSelected", "profile", "posted", "answered", "deal", "completed", "reviewed"], all["business"].Select(step => step.Key));
        // Two applications are one person reaching out; the drafter never got past the profile.
        Assert.Equal([9, 5, 4, 2, 1, 1, 1], Growth(allBefore, all, "blogger"));
        Assert.Equal([9, 1, 1, 1, 0, 0, 0], Growth(allBefore, all, "brandFace"));
        Assert.Equal([9, 2, 2, 1, 1, 1, 1, 1], Growth(allBefore, all, "business"));

        // The blogger who first opened the app 40 days ago falls outside the last 30 days, with every step after it.
        var month = await GetFunnelAsync(adminClient, "30d");
        Assert.Equal([8, 4, 3, 2, 1, 1, 1], Growth(monthBefore, month, "blogger"));
        Assert.Equal([8, 1, 1, 1, 0, 0, 0], Growth(monthBefore, month, "brandFace"));
        Assert.Equal([8, 2, 2, 1, 1, 1, 1, 1], Growth(monthBefore, month, "business"));

        using var memberClient = CreateClient(1_100_961);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/admin/funnel")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await adminClient.GetAsync("/api/admin/funnel?period=1d")).StatusCode);
    }

    [IntegrationFact]
    public async Task Ad_label_and_app_opens_reach_the_admin_dashboard()
    {
        const long adminTelegramUserId = 1_100_980;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var admin = PlatformUser.Create(adminTelegramUserId, "Activity admin", null);
            admin.SetRole(PlatformRole.Admin);
            dbContext.Add(admin);
            await dbContext.SaveChangesAsync();
        }

        using var newcomer = CreateClient(1_100_981);
        Assert.Equal(HttpStatusCode.OK, (await newcomer.GetAsync("/api/users/me?source=ci_label")).StatusCode);
        // A second link later does not move the account to another label.
        Assert.Equal(HttpStatusCode.OK, (await newcomer.GetAsync("/api/users/me?source=other_label")).StatusCode);

        using var adminClient = CreateClient(adminTelegramUserId);
        var dashboard = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/dashboard");
        var sources = dashboard.GetProperty("sources").EnumerateArray().ToDictionary(item => item.GetProperty("source").GetString()!, item => item.GetProperty("users").GetInt32());
        Assert.Equal(1, sources["ci_label"]);
        Assert.False(sources.ContainsKey("other_label"));
        Assert.True(dashboard.GetProperty("activeToday").GetInt32() >= 1);
        Assert.True(dashboard.GetProperty("activeWeek").GetInt32() >= dashboard.GetProperty("activeToday").GetInt32());
    }

    [IntegrationFact]
    public async Task Csp_reports_reach_only_admins()
    {
        const long adminTelegramUserId = 1_100_990;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
            var admin = PlatformUser.Create(adminTelegramUserId, "Csp admin", null);
            admin.SetRole(PlatformRole.Admin);
            dbContext.AddRange(admin, PlatformUser.Create(1_100_991, "Csp member", null));
            await dbContext.SaveChangesAsync();
        }

        using var member = CreateClient(1_100_991);
        var sent = await member.PostAsJsonAsync("/api/csp-reports", new { directive = "script-src-elem", blocked = "https://evil.example/x.js?k=1", source = "https://app.example/" });
        Assert.Equal(HttpStatusCode.NoContent, sent.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/admin/csp-reports")).StatusCode);

        using var adminClient = CreateClient(adminTelegramUserId);
        var reports = await adminClient.GetFromJsonAsync<JsonElement>("/api/admin/csp-reports");
        Assert.Contains(reports.EnumerateArray(), report => report.GetProperty("blocked").GetString() == "https://evil.example/x.js");
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

    private async Task<SeededApplication> SeedPendingApplicationAsync(long bloggerTelegramUserId, long businessTelegramUserId)
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
        var campaign = Campaign.Create(business.Id, "Pending title", "Pending description", ["beauty"], ["One reel"], 100, 200, "tashkent", null);
        campaign.Publish();
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        dbContext.AddRange(bloggerUser, businessUser, blogger, business, campaign, application);
        await dbContext.SaveChangesAsync();
        return new SeededApplication(campaign.Id, application.Id, blogger.Id);
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

    // Steps of each role in order; every step must be a subset of the one before it.
    private static async Task<Dictionary<string, (string Key, int Count)[]>> GetFunnelAsync(HttpClient client, string period)
    {
        var funnel = await client.GetFromJsonAsync<JsonElement>($"/api/admin/funnel?period={period}");
        Assert.Equal(period, funnel.GetProperty("period").GetString());
        var roles = funnel.GetProperty("roles").EnumerateArray().ToDictionary(
            role => role.GetProperty("role").GetString()!,
            role => role.GetProperty("steps").EnumerateArray().Select(step => (Key: step.GetProperty("key").GetString()!, Count: step.GetProperty("count").GetInt32())).ToArray());
        foreach (var steps in roles.Values)
        {
            for (var index = 1; index < steps.Length; index++)
            {
                Assert.True(steps[index].Count <= steps[index - 1].Count, $"{steps[index].Key} has more people than {steps[index - 1].Key}");
            }
        }

        return roles;
    }

    private static int[] Growth(Dictionary<string, (string Key, int Count)[]> before, Dictionary<string, (string Key, int Count)[]> after, string role) =>
        after[role].Select((step, index) => step.Count - before[role][index].Count).ToArray();

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

    private sealed record SeededApplication(Guid CampaignId, Guid ApplicationId, Guid BloggerId);

    private sealed record SeededDeal(Guid DealId, Guid CampaignId, Guid ApplicationId, Guid BloggerId, long BloggerTelegramUserId, long BusinessTelegramUserId, Guid BusinessId);
}
