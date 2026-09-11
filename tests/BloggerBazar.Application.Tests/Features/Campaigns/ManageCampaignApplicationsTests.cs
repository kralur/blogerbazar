using System.Reflection;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class ManageCampaignApplicationsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void Application_list_rejects_unknown_status(int status)
    {
        var result = new GetMyCampaignApplicationsPageValidator().Validate(new GetMyCampaignApplicationsPageQuery(1, status, 1, 20));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void Application_list_rejects_invalid_pagination(int page, int pageSize)
    {
        var result = new GetMyCampaignApplicationsPageValidator().Validate(new GetMyCampaignApplicationsPageQuery(1, null, page, pageSize));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(MarketplaceRole.Business)]
    [InlineData(MarketplaceRole.BrandFace)]
    public async Task Mine_list_forbids_non_blogger_active_roles(MarketplaceRole role)
    {
        var user = User(10, role);
        var blogger = ApprovedBlogger(10);
        var handler = new GetMyCampaignApplicationsPageHandler(new Users(user), new Bloggers(blogger), new CapturingReadModel());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new GetMyCampaignApplicationsPageQuery(10, null, 1, 20), CancellationToken.None));
    }

    [Fact]
    public async Task Mine_list_is_scoped_to_the_server_side_blogger_profile()
    {
        var blogger = ApprovedBlogger(11);
        var readModel = new CapturingReadModel();
        var handler = new GetMyCampaignApplicationsPageHandler(new Users(User(11, MarketplaceRole.Blogger)), new Bloggers(blogger), readModel);

        await handler.Handle(new GetMyCampaignApplicationsPageQuery(11, (int)CampaignApplicationStatus.Viewed, 2, 10), CancellationToken.None);

        Assert.Equal(blogger.Id, readModel.BloggerId);
        Assert.Equal((int)CampaignApplicationStatus.Viewed, readModel.Search?.Status);
        Assert.Equal(2, readModel.Search?.Page);
        Assert.Equal(10, readModel.Search?.PageSize);
    }

    [Fact]
    public async Task Mine_details_are_scoped_to_the_server_side_blogger_profile()
    {
        var blogger = ApprovedBlogger(15);
        var readModel = new CapturingReadModel();
        var handler = new GetMyCampaignApplicationDetailsHandler(new Users(User(15, MarketplaceRole.Blogger)), new Bloggers(blogger), readModel);

        await handler.Handle(new GetMyCampaignApplicationDetailsQuery(15, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(blogger.Id, readModel.DetailsBloggerId);
    }

    [Theory]
    [InlineData(MarketplaceRole.Business)]
    [InlineData(MarketplaceRole.BrandFace)]
    public async Task Apply_forbids_non_blogger_active_roles(MarketplaceRole role)
    {
        var business = ApprovedBusiness(16);
        var campaign = PublishedCampaign(business);
        var blogger = ApprovedBlogger(17);
        var handler = new ApplyToCampaignHandler(new Campaigns(campaign), new Users(User(17, role), User(16, MarketplaceRole.Business)), new Bloggers(blogger), new Businesses(), new Applications(), new CountingUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new ApplyToCampaignCommand(campaign.Id, 17, null), CancellationToken.None));
    }

    [Fact]
    public async Task Successful_apply_invalidates_only_campaign_catalog_after_persistence()
    {
        var business = ApprovedBusiness(18);
        var campaign = PublishedCampaign(business);
        var blogger = ApprovedBlogger(19);
        var cache = new RecordingCache();
        var handler = new ApplyToCampaignHandler(new Campaigns(campaign), new Users(User(19, MarketplaceRole.Blogger), User(18, MarketplaceRole.Business)), new Bloggers(blogger), new Businesses(), new Applications(), new CountingUnitOfWork(), cache);

        await handler.Handle(new ApplyToCampaignCommand(campaign.Id, 19, null), CancellationToken.None);

        Assert.Equal(["campaigns"], cache.Namespaces);
    }

    [Theory]
    [InlineData(CampaignStatus.Draft)]
    [InlineData(CampaignStatus.Archived)]
    public async Task Apply_rejects_non_published_campaigns(CampaignStatus status)
    {
        var business = ApprovedBusiness(60);
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, "tashkent", null);
        if (status == CampaignStatus.Archived) campaign.Archive();
        Attach(campaign, nameof(Campaign.Business), business);
        var blogger = ApprovedBlogger(61);
        var handler = new ApplyToCampaignHandler(new Campaigns(campaign), new Users(User(61, MarketplaceRole.Blogger), User(60, MarketplaceRole.Business)), new Bloggers(blogger), new Businesses(), new Applications(), new CountingUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ApplyToCampaignCommand(campaign.Id, 61, null), CancellationToken.None));
    }

    [Fact]
    public async Task Apply_returns_conflict_when_database_unique_constraint_wins_a_duplicate_race()
    {
        var business = ApprovedBusiness(62);
        var campaign = PublishedCampaign(business);
        var blogger = ApprovedBlogger(63);
        var handler = new ApplyToCampaignHandler(new Campaigns(campaign), new Users(User(63, MarketplaceRole.Blogger), User(62, MarketplaceRole.Business)), new Bloggers(blogger), new Businesses(), new Applications(), new UniqueConflictUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ApplyToCampaignCommand(campaign.Id, 63, null), CancellationToken.None));

        Assert.Contains("already applied", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Apply_returns_not_found_contract_for_a_missing_campaign()
    {
        var blogger = ApprovedBlogger(64);
        var handler = new ApplyToCampaignHandler(new Campaigns(), new Users(User(64, MarketplaceRole.Blogger)), new Bloggers(blogger), new Businesses(), new Applications(), new CountingUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ApplyToCampaignCommand(Guid.NewGuid(), 64, null), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Withdraw_is_owner_scoped_and_idempotent_for_a_withdrawn_application()
    {
        var blogger = ApprovedBlogger(12);
        var other = ApprovedBlogger(13);
        var application = ApplicationFor(blogger);
        var repository = new Applications(application);
        var unitOfWork = new CountingUnitOfWork();
        var handler = new WithdrawMyCampaignApplicationHandler(new Users(User(12, MarketplaceRole.Blogger)), new Bloggers(blogger, other), repository, unitOfWork);

        var first = await handler.Handle(new WithdrawMyCampaignApplicationCommand(12, application.Id), CancellationToken.None);
        var second = await handler.Handle(new WithdrawMyCampaignApplicationCommand(12, application.Id), CancellationToken.None);

        Assert.Equal((int)CampaignApplicationStatus.Withdrawn, first.Status);
        Assert.Equal((int)CampaignApplicationStatus.Withdrawn, second.Status);
        Assert.Equal(1, unitOfWork.SaveCount);

        var otherHandler = new WithdrawMyCampaignApplicationHandler(new Users(User(13, MarketplaceRole.Blogger)), new Bloggers(blogger, other), repository, new CountingUnitOfWork());
        await Assert.ThrowsAsync<InvalidOperationException>(() => otherHandler.Handle(new WithdrawMyCampaignApplicationCommand(13, application.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Withdraw_rejects_final_decisions_without_saving()
    {
        var blogger = ApprovedBlogger(14);
        var application = ApplicationFor(blogger);
        application.Reject();
        var unitOfWork = new CountingUnitOfWork();
        var handler = new WithdrawMyCampaignApplicationHandler(new Users(User(14, MarketplaceRole.Blogger)), new Bloggers(blogger), new Applications(application), unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new WithdrawMyCampaignApplicationCommand(14, application.Id), CancellationToken.None));

        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Owner_accept_creates_one_deal_and_is_idempotent()
    {
        var business = ApprovedBusiness(20);
        var campaign = PublishedCampaign(business);
        var blogger = ApprovedBlogger(21);
        var application = ApplicationFor(blogger, campaign);
        var deals = new Deals();
        var handler = DecisionHandler(20, business, campaign, application, blogger, deals, new CountingUnitOfWork());

        var first = await handler.Handle(new DecideCampaignApplicationCommand(20, campaign.Id, application.Id, CampaignApplicationStatus.Accepted), CancellationToken.None);
        var second = await handler.Handle(new DecideCampaignApplicationCommand(20, campaign.Id, application.Id, CampaignApplicationStatus.Accepted), CancellationToken.None);

        Assert.Equal((int)CampaignApplicationStatus.Accepted, first.Status);
        Assert.Equal(first.DealId, second.DealId);
        Assert.Single(deals.Values);
    }

    [Fact]
    public async Task Concurrent_accept_unique_conflict_returns_the_single_persisted_deal()
    {
        var business = ApprovedBusiness(25);
        var campaign = PublishedCampaign(business);
        var blogger = ApprovedBlogger(26);
        var application = ApplicationFor(blogger, campaign);
        var deals = new ConcurrentRaceDeals();
        var unitOfWork = new UniqueConflictUnitOfWork(() => deals.PersistCompetingDeal(application.Id, blogger.Id, business.Id));
        var handler = DecisionHandler(25, business, campaign, application, blogger, deals, unitOfWork);

        var result = await handler.Handle(new DecideCampaignApplicationCommand(25, campaign.Id, application.Id, CampaignApplicationStatus.Accepted), CancellationToken.None);

        Assert.Equal((int)CampaignApplicationStatus.Accepted, result.Status);
        Assert.NotNull(result.DealId);
        Assert.Single(deals.Persisted);
        Assert.Equal(deals.Persisted[0].Id, result.DealId);
    }

    [Fact]
    public async Task Owner_reject_is_idempotent_but_conflicting_accept_returns_conflict()
    {
        var business = ApprovedBusiness(30);
        var campaign = PublishedCampaign(business);
        var blogger = ApprovedBlogger(31);
        var application = ApplicationFor(blogger, campaign);
        var handler = DecisionHandler(30, business, campaign, application, blogger, new Deals(), new CountingUnitOfWork());

        var first = await handler.Handle(new DecideCampaignApplicationCommand(30, campaign.Id, application.Id, CampaignApplicationStatus.Rejected), CancellationToken.None);
        var second = await handler.Handle(new DecideCampaignApplicationCommand(30, campaign.Id, application.Id, CampaignApplicationStatus.Rejected), CancellationToken.None);

        Assert.Equal((int)CampaignApplicationStatus.Rejected, first.Status);
        Assert.Equal((int)CampaignApplicationStatus.Rejected, second.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new DecideCampaignApplicationCommand(30, campaign.Id, application.Id, CampaignApplicationStatus.Accepted), CancellationToken.None));
    }

    [Fact]
    public async Task Owner_cannot_decide_an_application_from_another_campaign()
    {
        var business = ApprovedBusiness(40);
        var campaign = PublishedCampaign(business);
        var otherCampaign = PublishedCampaign(business);
        var blogger = ApprovedBlogger(41);
        var application = ApplicationFor(blogger, otherCampaign);
        var handler = DecisionHandler(40, business, campaign, application, blogger, new Deals(), new CountingUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new DecideCampaignApplicationCommand(40, campaign.Id, application.Id, CampaignApplicationStatus.Rejected), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Business_inbox_is_scoped_to_the_owned_campaign()
    {
        var business = ApprovedBusiness(50);
        var campaign = PublishedCampaign(business);
        var readModel = new CapturingReadModel();
        var handler = new GetCampaignApplicationInboxHandler(new Users(User(50, MarketplaceRole.Business)), new Businesses(business), new Campaigns(campaign), readModel);

        await handler.Handle(new GetCampaignApplicationInboxQuery(50, campaign.Id, (int)CampaignApplicationStatus.Sent, 3, 5), CancellationToken.None);

        Assert.Equal(business.Id, readModel.BusinessId);
        Assert.Equal(campaign.Id, readModel.CampaignId);
        Assert.Equal((int)CampaignApplicationStatus.Sent, readModel.Search?.Status);
    }

    private static DecideCampaignApplicationHandler DecisionHandler(long telegramUserId, BusinessProfile business, Campaign campaign, CampaignApplication application, BloggerProfile blogger, IDealRepository deals, IUnitOfWork unitOfWork) =>
        new(new Users(User(telegramUserId, MarketplaceRole.Business)), new Businesses(business), new Campaigns(campaign), new Applications(application), deals, unitOfWork, new Bloggers(blogger));

    private static PlatformUser User(long telegramUserId, MarketplaceRole role)
    {
        var user = PlatformUser.Create(telegramUserId, "User", null);
        user.SelectMarketplaceRole(role);
        return user;
    }

    private static BloggerProfile ApprovedBlogger(long telegramUserId)
    {
        var blogger = BloggerProfile.Create(telegramUserId, "Blogger", "tashkent", ["beauty"]);
        blogger.Approve();
        return blogger;
    }

    private static BusinessProfile ApprovedBusiness(long telegramUserId)
    {
        var business = BusinessProfile.Create(telegramUserId, "Business", "tashkent");
        business.Approve();
        return business;
    }

    private static Campaign PublishedCampaign(BusinessProfile business)
    {
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, "tashkent", null);
        campaign.Publish();
        Attach(campaign, nameof(Campaign.Business), business);
        return campaign;
    }

    private static CampaignApplication ApplicationFor(BloggerProfile blogger, Campaign? campaign = null)
    {
        campaign ??= PublishedCampaign(ApprovedBusiness(99));
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        Attach(application, nameof(CampaignApplication.Campaign), campaign);
        return application;
    }

    private static void Attach<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);

    private sealed class Users(params PlatformUser[] values) : IPlatformUserRepository
    {
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(values.Count(user => !user.IsDeleted));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(values.Take(take).ToArray());
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(values.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
    }

    private sealed class Bloggers(params BloggerProfile[] values) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(values.SingleOrDefault(profile => profile.Id == id));
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(values.SingleOrDefault(profile => profile.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class Businesses(params BusinessProfile[] values) : IBusinessProfileRepository
    {
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(values.SingleOrDefault(profile => profile.Id == id));
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(values.SingleOrDefault(profile => profile.TelegramUserId == telegramUserId));
    }

    private sealed class Campaigns(params Campaign[] values) : ICampaignRepository
    {
        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(values.SingleOrDefault(campaign => campaign.Id == id));
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult(values.SingleOrDefault(campaign => campaign.Id == id && campaign.BusinessId == businessId));
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class Applications(params CampaignApplication[] values) : ICampaignApplicationRepository
    {
        public Task AddAsync(CampaignApplication application, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid campaignId, Guid bloggerId, CancellationToken cancellationToken) => Task.FromResult(values.Any(application => application.CampaignId == campaignId && application.BloggerId == bloggerId));
        public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(values.SingleOrDefault(application => application.Id == id));
    }

    private sealed class Deals : IDealRepository
    {
        public List<Deal> Values { get; } = [];
        public Task AddAsync(Deal deal, CancellationToken cancellationToken) { Values.Add(deal); return Task.CompletedTask; }
        public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(Values.Any(deal => deal.CampaignApplicationId == campaignApplicationId));
        public Task<Deal?> GetByCampaignApplicationIdAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(Values.SingleOrDefault(deal => deal.CampaignApplicationId == campaignApplicationId));
        public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Values.SingleOrDefault(deal => deal.Id == id));
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.FromResult(1); }
    }

    private sealed class UniqueConflictUnitOfWork(Action? onUniqueConflict = null) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            onUniqueConflict?.Invoke();
            return Task.FromResult(false);
        }
    }

    private sealed class CapturingReadModel : ICampaignApplicationReadModel
    {
        public Guid? BloggerId { get; private set; }
        public Guid? DetailsBloggerId { get; private set; }
        public Guid? BusinessId { get; private set; }
        public Guid? CampaignId { get; private set; }
        public CampaignApplicationSearch? Search { get; private set; }
        public Task<MyCampaignApplicationDetailsDto?> GetForBloggerAsync(Guid bloggerId, Guid applicationId, CancellationToken cancellationToken)
        {
            DetailsBloggerId = bloggerId;
            return Task.FromResult<MyCampaignApplicationDetailsDto?>(null);
        }

        public Task<CampaignApplicationInboxResult> SearchForBusinessAsync(Guid businessId, Guid campaignId, CampaignApplicationSearch search, CancellationToken cancellationToken)
        {
            BusinessId = businessId;
            CampaignId = campaignId;
            Search = search;
            return Task.FromResult(new CampaignApplicationInboxResult([], 0, search.Page, search.PageSize, false));
        }
        public Task<MyCampaignApplicationsResult> SearchForBloggerAsync(Guid bloggerId, CampaignApplicationSearch search, CancellationToken cancellationToken)
        {
            BloggerId = bloggerId;
            Search = search;
            return Task.FromResult(new MyCampaignApplicationsResult([], 0, search.Page, search.PageSize, false));
        }
    }

    private sealed class RecordingCache : ICatalogCache
    {
        public List<string> Namespaces { get; } = [];
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class => Task.FromResult<T?>(null);
        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
        public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RotateNamespaceVersionAsync(string catalog, CancellationToken cancellationToken) { Namespaces.Add(catalog); return Task.CompletedTask; }
    }

    private sealed class ConcurrentRaceDeals : IDealRepository
    {
        public List<Deal> Persisted { get; } = [];
        public Task AddAsync(Deal deal, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(Persisted.Any(deal => deal.CampaignApplicationId == campaignApplicationId));
        public Task<Deal?> GetByCampaignApplicationIdAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(Persisted.SingleOrDefault(deal => deal.CampaignApplicationId == campaignApplicationId));
        public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Persisted.SingleOrDefault(deal => deal.Id == id));
        public void PersistCompetingDeal(Guid applicationId, Guid bloggerId, Guid businessId) => Persisted.Add(Deal.Create(applicationId, bloggerId, businessId));
    }
}
