using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

// D69: a draft or closed campaign is planned for a moment; a background job publishes it then and tells the owner.
public sealed class CampaignScheduleTests
{
    private const long Owner = 10;

    [Fact]
    public async Task Owner_plans_a_draft_and_it_stays_a_draft_until_then()
    {
        var (campaign, handler) = ScheduleSetup(CampaignStatus.Draft);
        var at = DateTime.UtcNow.AddDays(1);

        var result = await handler.Handle(new ScheduleCampaignCommand(campaign.Id, Owner, at), CancellationToken.None);

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(at, campaign.ScheduledPublishAtUtc);
        Assert.Equal(at, result.ScheduledPublishAtUtc);
        Assert.Equal((int)CampaignStatus.Draft, result.Status);
    }

    [Fact]
    public async Task A_closed_campaign_can_be_planned_to_come_back()
    {
        var (campaign, handler) = ScheduleSetup(CampaignStatus.Archived);

        await handler.Handle(new ScheduleCampaignCommand(campaign.Id, Owner, DateTime.UtcNow.AddHours(2)), CancellationToken.None);

        Assert.NotNull(campaign.ScheduledPublishAtUtc);
    }

    [Fact]
    public async Task Published_rejected_and_expired_campaigns_cannot_be_planned()
    {
        var (published, publishedHandler) = ScheduleSetup(CampaignStatus.Published);
        var conflict = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => publishedHandler.Handle(new ScheduleCampaignCommand(published.Id, Owner, DateTime.UtcNow.AddDays(1)), CancellationToken.None));
        Assert.Equal("campaign_already_published", conflict.Code);

        var (rejected, rejectedHandler) = ScheduleSetup(CampaignStatus.Rejected);
        var rejection = await Assert.ThrowsAsync<InvalidOperationException>(() => rejectedHandler.Handle(new ScheduleCampaignCommand(rejected.Id, Owner, DateTime.UtcNow.AddDays(1)), CancellationToken.None));
        Assert.DoesNotContain("not found", rejection.Message, StringComparison.OrdinalIgnoreCase);

        // The deadline is tomorrow, the plan is for the day after: it would go out already expired.
        var (expiring, expiringHandler) = ScheduleSetup(CampaignStatus.Draft, deadline: DateTime.UtcNow.Date.AddDays(1));
        var expired = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => expiringHandler.Handle(new ScheduleCampaignCommand(expiring.Id, Owner, DateTime.UtcNow.Date.AddDays(2).AddHours(6)), CancellationToken.None));
        Assert.Equal("campaign_expired", expired.Code);
        Assert.Null(expiring.ScheduledPublishAtUtc);
    }

    [Fact]
    public async Task Another_company_cannot_plan_or_cancel_the_campaign()
    {
        var owner = Business(Owner);
        var other = Business(20);
        var campaign = CampaignFor(owner, CampaignStatus.Draft);
        var campaigns = new CampaignRepository(campaign);
        var users = new UserRepository(User(20));
        var businesses = new BusinessRepository(other);

        var schedule = await Assert.ThrowsAsync<InvalidOperationException>(() => new ScheduleCampaignHandler(campaigns, users, businesses, new SpyUnitOfWork()).Handle(new ScheduleCampaignCommand(campaign.Id, 20, DateTime.UtcNow.AddDays(1)), CancellationToken.None));
        var cancel = await Assert.ThrowsAsync<InvalidOperationException>(() => new CancelCampaignScheduleHandler(campaigns, users, businesses, new SpyUnitOfWork()).Handle(new CancelCampaignScheduleCommand(campaign.Id, 20), CancellationToken.None));

        Assert.Contains("not found", schedule.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not found", cancel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(campaign.ScheduledPublishAtUtc);
    }

    [Fact]
    public async Task An_account_working_as_a_blogger_cannot_plan_its_company_campaign()
    {
        var business = Business(Owner);
        var campaign = CampaignFor(business, CampaignStatus.Draft);
        var blogger = PlatformUser.Create(Owner, "Blogger", null);
        blogger.SelectMarketplaceRole(MarketplaceRole.Blogger);
        var handler = new ScheduleCampaignHandler(new CampaignRepository(campaign), new UserRepository(blogger), new BusinessRepository(business), new SpyUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new ScheduleCampaignCommand(campaign.Id, Owner, DateTime.UtcNow.AddDays(1)), CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_drops_the_plan_and_keeps_the_draft()
    {
        var business = Business(Owner);
        var campaign = CampaignFor(business, CampaignStatus.Draft);
        campaign.SchedulePublication(DateTime.UtcNow.AddDays(1));
        var handler = new CancelCampaignScheduleHandler(new CampaignRepository(campaign), new UserRepository(User(Owner)), new BusinessRepository(business), new SpyUnitOfWork());

        var result = await handler.Handle(new CancelCampaignScheduleCommand(campaign.Id, Owner), CancellationToken.None);

        Assert.Null(campaign.ScheduledPublishAtUtc);
        Assert.Null(result.ScheduledPublishAtUtc);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
    }

    [Fact]
    public void Publishing_closing_or_an_admin_decision_ends_the_plan()
    {
        foreach (var change in new Action<Campaign>[] { campaign => campaign.Publish(), campaign => campaign.Archive(), campaign => campaign.SetStatus(CampaignStatus.Rejected) })
        {
            var campaign = CampaignFor(Business(Owner), CampaignStatus.Draft);
            campaign.SchedulePublication(DateTime.UtcNow.AddDays(1));

            change(campaign);

            Assert.Null(campaign.ScheduledPublishAtUtc);
        }
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(0)]
    [InlineData(60 * 24 * 61)]
    public void The_time_must_be_from_a_minute_to_sixty_days_ahead(int minutesFromNow)
    {
        var result = new ScheduleCampaignValidator().Validate(new ScheduleCampaignCommand(Guid.NewGuid(), Owner, DateTime.UtcNow.AddMinutes(minutesFromNow)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_time_tomorrow_is_accepted()
    {
        Assert.True(new ScheduleCampaignValidator().Validate(new ScheduleCampaignCommand(Guid.NewGuid(), Owner, DateTime.UtcNow.AddDays(1))).IsValid);
    }

    [Fact]
    public void A_new_campaign_is_not_both_published_now_and_planned()
    {
        var validator = new CreateCampaignValidator();
        var both = Create(publishNow: true, DateTime.UtcNow.AddDays(1));
        var planned = Create(publishNow: false, DateTime.UtcNow.AddDays(1));
        var past = Create(publishNow: false, DateTime.UtcNow.AddHours(-1));

        Assert.False(validator.Validate(both).IsValid);
        Assert.True(validator.Validate(planned).IsValid);
        Assert.False(validator.Validate(past).IsValid);
    }

    [Fact]
    public async Task A_new_campaign_can_be_created_already_planned()
    {
        var business = Business(Owner);
        var campaigns = new CampaignRepository();
        var handler = new CreateCampaignHandler(new BusinessRepository(business), campaigns, new SpyUnitOfWork(), users: new UserRepository(User(Owner)));
        var at = DateTime.UtcNow.AddDays(1);

        var result = await handler.Handle(Create(publishNow: false, at), CancellationToken.None);

        var campaign = Assert.Single(campaigns.All);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(at, campaign.ScheduledPublishAtUtc);
        Assert.Equal(at, result.ScheduledPublishAtUtc);
    }

    [Fact]
    public async Task The_job_publishes_due_campaigns_tells_the_owner_and_refreshes_the_catalog()
    {
        var now = new DateTime(2026, 11, 22, 6, 0, 0, DateTimeKind.Utc);
        var due = new DueScheduledCampaign(Guid.NewGuid(), "Autumn menu", now.AddMinutes(-1), null, 555);
        var repository = new DueRepository(due);
        var bot = new SpyBotClient();
        var cache = new RecordingCache();

        var published = await new PublishScheduledCampaignsHandler(repository, cache, bot).Handle(new PublishScheduledCampaignsCommand(now), CancellationToken.None);

        Assert.Equal(1, published);
        Assert.Equal([due.Id], repository.Published);
        Assert.Equal([555L], bot.NotifiedChats);
        Assert.Contains("Autumn menu", bot.Texts.Single());
        Assert.Equal($"/my-campaign/{due.Id}", bot.Routes.Single());
        Assert.Equal(["campaigns"], cache.RotatedCatalogs);
    }

    [Fact]
    public async Task A_plan_whose_deadline_has_passed_is_dropped_and_the_owner_is_told_why()
    {
        var now = new DateTime(2026, 11, 22, 6, 0, 0, DateTimeKind.Utc);
        var due = new DueScheduledCampaign(Guid.NewGuid(), "Old promo", now.AddMinutes(-1), now.Date.AddDays(-1), 555);
        var repository = new DueRepository(due);
        var bot = new SpyBotClient();
        var cache = new RecordingCache();

        var published = await new PublishScheduledCampaignsHandler(repository, cache, bot).Handle(new PublishScheduledCampaignsCommand(now), CancellationToken.None);

        Assert.Equal(0, published);
        Assert.Empty(repository.Published);
        Assert.Equal([due.Id], repository.Dropped);
        Assert.Contains("срок кампании уже прошёл", bot.Texts.Single());
        Assert.Empty(cache.RotatedCatalogs);
    }

    [Fact]
    public async Task A_plan_the_owner_changed_meanwhile_is_left_alone_and_nobody_is_told()
    {
        var now = new DateTime(2026, 11, 22, 6, 0, 0, DateTimeKind.Utc);
        var repository = new DueRepository(new DueScheduledCampaign(Guid.NewGuid(), "Changed", now.AddMinutes(-1), null, 555)) { Unchanged = false };
        var bot = new SpyBotClient();

        var published = await new PublishScheduledCampaignsHandler(repository, null, bot).Handle(new PublishScheduledCampaignsCommand(now), CancellationToken.None);

        Assert.Equal(0, published);
        Assert.Empty(bot.NotifiedChats);
    }

    private static CreateCampaignCommand Create(bool publishNow, DateTime? at) =>
        new(Owner, "Title", "Description", "tashkent", ["beauty"], null, 100, 200, null, publishNow, at);

    private static (Campaign Campaign, ScheduleCampaignHandler Handler) ScheduleSetup(CampaignStatus status, DateTime? deadline = null)
    {
        var business = Business(Owner);
        var campaign = CampaignFor(business, status, deadline);
        return (campaign, new ScheduleCampaignHandler(new CampaignRepository(campaign), new UserRepository(User(Owner)), new BusinessRepository(business), new SpyUnitOfWork()));
    }

    private static BusinessProfile Business(long telegramUserId)
    {
        var business = BusinessProfile.Create(telegramUserId, "Business", "tashkent");
        business.Approve();
        return business;
    }

    private static PlatformUser User(long telegramUserId)
    {
        var user = PlatformUser.Create(telegramUserId, "Business", null);
        user.SelectMarketplaceRole(MarketplaceRole.Business);
        return user;
    }

    private static Campaign CampaignFor(BusinessProfile business, CampaignStatus status, DateTime? deadline = null)
    {
        var campaign = Campaign.Create(business.Id, "Title", "Description", ["beauty"], null, 100, 200, "tashkent", deadline);
        campaign.SetStatus(status);
        return campaign;
    }

    private sealed class CampaignRepository(params Campaign[] initial) : ICampaignRepository
    {
        private readonly List<Campaign> campaigns = [.. initial];
        public IReadOnlyList<Campaign> All => campaigns;
        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken) { campaigns.Add(campaign); return Task.CompletedTask; }
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(campaigns.SingleOrDefault(campaign => campaign.Id == id));
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult(campaigns.SingleOrDefault(campaign => campaign.Id == id && campaign.BusinessId == businessId));
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class DueRepository(params DueScheduledCampaign[] due) : ICampaignRepository
    {
        public bool Unchanged { get; init; } = true;
        public List<Guid> Published { get; } = [];
        public List<Guid> Dropped { get; } = [];
        public Task<IReadOnlyList<DueScheduledCampaign>> ListDueScheduledAsync(DateTime nowUtc, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DueScheduledCampaign>>(due);
        public Task<bool> TryPublishScheduledAsync(Guid id, DateTime scheduledAtUtc, DateTime nowUtc, CancellationToken cancellationToken) { if (Unchanged) Published.Add(id); return Task.FromResult(Unchanged); }
        public Task<bool> TryDropScheduleAsync(Guid id, DateTime scheduledAtUtc, DateTime nowUtc, CancellationToken cancellationToken) { if (Unchanged) Dropped.Add(id); return Task.FromResult(Unchanged); }
        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Campaign?>(null);
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult<Campaign?>(null);
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class BusinessRepository(params BusinessProfile[] initial) : IBusinessProfileRepository
    {
        private readonly List<BusinessProfile> businesses = [.. initial];
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) { businesses.Add(profile); return Task.CompletedTask; }
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(businesses.SingleOrDefault(business => business.Id == id && !business.IsDeleted));
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(businesses.SingleOrDefault(business => business.TelegramUserId == telegramUserId && !business.IsDeleted));
    }

    private sealed class UserRepository(params PlatformUser[] initial) : IPlatformUserRepository
    {
        private readonly List<PlatformUser> users = [.. initial];
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users.Where(user => !user.IsDeleted).Take(take).ToArray());
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Count(user => !user.IsDeleted));
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) { users.Add(user); return Task.CompletedTask; }
    }

    private sealed class RecordingCache : ICatalogCache
    {
        public List<string> RotatedCatalogs { get; } = [];
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class => Task.FromResult<T?>(null);
        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
        public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RotateNamespaceVersionAsync(string catalog, CancellationToken cancellationToken) { RotatedCatalogs.Add(catalog); return Task.CompletedTask; }
    }
}
