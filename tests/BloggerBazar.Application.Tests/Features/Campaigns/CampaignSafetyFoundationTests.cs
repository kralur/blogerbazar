using System.Reflection;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CampaignSafetyFoundationTests
{
    [Fact]
    public void Public_campaign_visibility_requires_published_campaign_and_available_approved_business_owner()
    {
        var visibleBusiness = ApprovedBusiness(1, "Visible");
        var deletedBusiness = ApprovedBusiness(2, "Deleted");
        deletedBusiness.SoftDelete();
        var pendingBusiness = BusinessProfile.Create(3, "Pending", "tashkent");
        var blockedBusiness = ApprovedBusiness(4, "Blocked");
        var archivedBusiness = ApprovedBusiness(5, "Archived");
        var draftBusiness = ApprovedBusiness(6, "Draft");
        var rejectedCampaignBusiness = ApprovedBusiness(7, "Rejected campaign");
        var deletedOwnerBusiness = ApprovedBusiness(8, "Deleted owner");

        var visible = PublishedCampaign(visibleBusiness);
        var deleted = PublishedCampaign(deletedBusiness);
        var pending = PublishedCampaign(pendingBusiness);
        var blocked = PublishedCampaign(blockedBusiness);
        var archived = PublishedCampaign(archivedBusiness);
        archived.Archive();
        var draft = Campaign.Create(draftBusiness.Id, "Draft", "Description", ["beauty"], null, null, null, "tashkent", null);
        var rejectedCampaign = PublishedCampaign(rejectedCampaignBusiness);
        rejectedCampaign.SetStatus(CampaignStatus.Rejected);
        var deletedOwner = PublishedCampaign(deletedOwnerBusiness);

        var blockedOwner = PlatformUser.Create(blockedBusiness.TelegramUserId, "Blocked", null);
        blockedOwner.SetBlocked(true);
        var removedOwner = PlatformUser.Create(deletedOwnerBusiness.TelegramUserId, "Deleted", null);
        removedOwner.SoftDelete(removedOwner.TelegramUserId);
        var campaigns = new[] { visible, deleted, pending, blocked, archived, draft, rejectedCampaign, deletedOwner }.AsQueryable();
        var businesses = new[] { visibleBusiness, deletedBusiness, pendingBusiness, blockedBusiness, archivedBusiness, draftBusiness, rejectedCampaignBusiness, deletedOwnerBusiness }.AsQueryable();
        var users = new[] { blockedOwner, removedOwner }.AsQueryable();

        var result = MarketplaceCatalogVisibility.PublicCampaigns(campaigns, businesses, users).ToArray();

        Assert.Collection(result, item => Assert.Equal(visible.Id, item.Id));
    }

    [Fact]
    public async Task Successful_campaign_mutations_rotate_only_the_campaign_catalog_namespace()
    {
        var business = ApprovedBusiness(10, "Lumi");
        var campaign = PublishedCampaign(business);
        AttachBusiness(campaign, business);
        var campaigns = new InMemoryCampaignRepository(campaign);
        var cache = new RecordingCache();
        var unitOfWork = new UnitOfWork();
        var businesses = new InMemoryBusinessRepository(business);
        var users = new InMemoryPlatformUserRepository(BusinessUser(10));

        await new CreateCampaignHandler(businesses, campaigns, unitOfWork, cache)
            .Handle(new CreateCampaignCommand(10, "New", "Description", "tashkent", ["beauty"], null, null, null, null, true), CancellationToken.None);
        await new UpdateCampaignHandler(campaigns, users, businesses, unitOfWork, cache)
            .Handle(new UpdateCampaignCommand(campaign.Id, 10, "Updated", "Description", "tashkent", ["beauty"], null, null, null, null), CancellationToken.None);
        await new CloseCampaignHandler(campaigns, users, businesses, unitOfWork, cache)
            .Handle(new CloseCampaignCommand(campaign.Id, 10), CancellationToken.None);
        await new ModerateCampaignHandler(campaigns, new AllowAdminAccess(), new InMemoryAuditLogs(), unitOfWork, cache)
            .Handle(new ModerateCampaignCommand(campaign.Id, 99, CampaignStatus.Published, true), CancellationToken.None);

        Assert.Equal(new[] { "campaigns", "campaigns", "campaigns", "campaigns" }, cache.RotatedCatalogs);
        Assert.Empty(cache.GlobalRotations);
    }

    [Fact]
    public async Task Failed_campaign_mutation_does_not_rotate_campaign_cache()
    {
        var cache = new RecordingCache();
        var handler = new CreateCampaignHandler(new InMemoryBusinessRepository(), new InMemoryCampaignRepository(), new UnitOfWork(), cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateCampaignCommand(10, "New", "Description", "tashkent", ["beauty"], null, null, null, null, true),
            CancellationToken.None));

        Assert.Empty(cache.RotatedCatalogs);
    }

    [Fact]
    public async Task Patch_status_uses_loaded_blogger_data_and_returns_backend_status_value()
    {
        var business = ApprovedBusiness(11, "Business");
        var campaign = PublishedCampaign(business);
        AttachBusiness(campaign, business);
        var blogger = BloggerProfile.Create(12, "Blogger", "tashkent", ["beauty"]);
        blogger.Approve();
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, "Hello");
        AttachCampaign(application, campaign);
        var handler = new UpdateCampaignApplicationStatusHandler(
            new InMemoryApplicationRepository(application),
            new InMemoryBusinessRepository(business),
            new UnitOfWork(),
            new InMemoryBloggerRepository(blogger));

        var result = await handler.Handle(new UpdateCampaignApplicationStatusCommand(application.Id, business.TelegramUserId, CampaignApplicationStatus.Viewed), CancellationToken.None);

        Assert.Equal((int)CampaignApplicationStatus.Viewed, result.Status);
        Assert.Equal(blogger.Name, result.CounterpartyName);
    }

    [Fact]
    public async Task Patch_status_rejects_a_business_that_does_not_own_the_campaign()
    {
        var owner = ApprovedBusiness(11, "Owner");
        var otherBusiness = ApprovedBusiness(13, "Other");
        var campaign = PublishedCampaign(owner);
        AttachBusiness(campaign, owner);
        var blogger = BloggerProfile.Create(12, "Blogger", "tashkent", ["beauty"]);
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        AttachCampaign(application, campaign);
        var handler = new UpdateCampaignApplicationStatusHandler(
            new InMemoryApplicationRepository(application),
            new InMemoryBusinessRepository(otherBusiness),
            new UnitOfWork(),
            new InMemoryBloggerRepository(blogger));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new UpdateCampaignApplicationStatusCommand(application.Id, otherBusiness.TelegramUserId, CampaignApplicationStatus.Viewed),
            CancellationToken.None));
    }

    [Fact]
    public async Task Repeated_sequential_apply_does_not_create_a_second_application()
    {
        var business = ApprovedBusiness(31, "Business");
        var campaign = PublishedCampaign(business);
        var blogger = BloggerProfile.Create(32, "Blogger", "tashkent", ["beauty"]);
        blogger.Approve();
        var applications = new StatefulApplicationRepository();
        var handler = new ApplyToCampaignHandler(
            new InMemoryCampaignRepository(campaign),
            new InMemoryBloggerRepository(blogger),
            new InMemoryBusinessRepository(),
            applications,
            new UnitOfWork());

        await handler.Handle(new ApplyToCampaignCommand(campaign.Id, blogger.TelegramUserId, null), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new ApplyToCampaignCommand(campaign.Id, blogger.TelegramUserId, null),
            CancellationToken.None));
        Assert.Single(applications.Applications);
    }

    [Theory]
    [InlineData(CampaignApplicationStatus.Sent)]
    [InlineData(CampaignApplicationStatus.Viewed)]
    public async Task Sent_and_viewed_applications_can_be_accepted_once(CampaignApplicationStatus status)
    {
        var business = ApprovedBusiness(21, "Business");
        var campaign = PublishedCampaign(business);
        AttachBusiness(campaign, business);
        var blogger = BloggerProfile.Create(22, "Blogger", "tashkent", ["beauty"]);
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        AttachCampaign(application, campaign);
        if (status == CampaignApplicationStatus.Viewed) application.MarkViewed();
        var deals = new InMemoryDealRepository();
        var handler = new AcceptCampaignApplicationHandler(
            new InMemoryApplicationRepository(application),
            new InMemoryBusinessRepository(business),
            deals,
            new UnitOfWork(),
            new InMemoryBloggerRepository(blogger));

        await handler.Handle(new AcceptCampaignApplicationCommand(application.Id, business.TelegramUserId), CancellationToken.None);

        Assert.Single(deals.Deals);
        Assert.Equal(CampaignApplicationStatus.Accepted, application.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new AcceptCampaignApplicationCommand(application.Id, business.TelegramUserId), CancellationToken.None));
        Assert.Single(deals.Deals);
    }

    private static BusinessProfile ApprovedBusiness(long telegramUserId, string name)
    {
        var profile = BusinessProfile.Create(telegramUserId, name, "tashkent");
        profile.Approve();
        return profile;
    }

    private static PlatformUser BusinessUser(long telegramUserId)
    {
        var user = PlatformUser.Create(telegramUserId, "Business", null);
        user.SelectMarketplaceRole(MarketplaceRole.Business);
        return user;
    }

    private static Campaign PublishedCampaign(BusinessProfile business)
    {
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, "tashkent", null);
        campaign.Publish();
        return campaign;
    }

    private static void AttachBusiness(Campaign campaign, BusinessProfile business) =>
        typeof(Campaign).GetProperty(nameof(Campaign.Business), BindingFlags.Instance | BindingFlags.Public)!.SetValue(campaign, business);

    private static void AttachCampaign(CampaignApplication application, Campaign campaign) =>
        typeof(CampaignApplication).GetProperty(nameof(CampaignApplication.Campaign), BindingFlags.Instance | BindingFlags.Public)!.SetValue(application, campaign);

    private sealed class InMemoryCampaignRepository(params Campaign[] initial) : ICampaignRepository
    {
        private readonly List<Campaign> campaigns = [.. initial];
        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken) { campaigns.Add(campaign); return Task.CompletedTask; }
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(campaigns.SingleOrDefault(campaign => campaign.Id == id));
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult(campaigns.SingleOrDefault(campaign => campaign.Id == id && campaign.BusinessId == businessId));
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class InMemoryBusinessRepository(params BusinessProfile[] initial) : IBusinessProfileRepository
    {
        private readonly List<BusinessProfile> businesses = [.. initial];
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) { businesses.Add(profile); return Task.CompletedTask; }
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(businesses.SingleOrDefault(business => business.Id == id));
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(businesses.SingleOrDefault(business => business.TelegramUserId == telegramUserId));
    }

    private sealed class InMemoryPlatformUserRepository(params PlatformUser[] initial) : IPlatformUserRepository
    {
        private readonly List<PlatformUser> users = [.. initial];
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users.Where(user => !user.IsDeleted).Take(take).ToArray());
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Count(user => !user.IsDeleted));
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) { users.Add(user); return Task.CompletedTask; }
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile blogger) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == blogger.Id ? blogger : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(telegramUserId == blogger.TelegramUserId ? blogger : null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class InMemoryApplicationRepository(CampaignApplication application) : ICampaignApplicationRepository
    {
        public Task AddAsync(CampaignApplication value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid campaignId, Guid bloggerId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CampaignApplication?>(id == application.Id ? application : null);
    }

    private sealed class StatefulApplicationRepository : ICampaignApplicationRepository
    {
        public List<CampaignApplication> Applications { get; } = [];
        public Task AddAsync(CampaignApplication application, CancellationToken cancellationToken) { Applications.Add(application); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(Guid campaignId, Guid bloggerId, CancellationToken cancellationToken) =>
            Task.FromResult(Applications.Any(application => application.CampaignId == campaignId && application.BloggerId == bloggerId));
        public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<CampaignApplication?>(Applications.SingleOrDefault(application => application.Id == id));
    }

    private sealed class InMemoryDealRepository : IDealRepository
    {
        public List<Deal> Deals { get; } = [];
        public Task AddAsync(Deal deal, CancellationToken cancellationToken) { Deals.Add(deal); return Task.CompletedTask; }
        public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(Deals.Any(deal => deal.CampaignApplicationId == campaignApplicationId));
        public Task<bool> ExistsForCollaborationRequestAsync(Guid collaborationRequestId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Deals.SingleOrDefault(deal => deal.Id == id));
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class RecordingCache : ICatalogCache
    {
        public List<string> RotatedCatalogs { get; } = [];
        public List<string> GlobalRotations { get; } = [];
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class => Task.FromResult<T?>(null);
        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
        public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) { GlobalRotations.Add("global"); return Task.CompletedTask; }
        public Task RotateNamespaceVersionAsync(string catalog, CancellationToken cancellationToken) { RotatedCatalogs.Add(catalog); return Task.CompletedTask; }
    }

    private sealed class AllowAdminAccess : IAdminAccessPolicy
    {
        public void EnsureAllowed(long telegramUserId) { }
    }

    private sealed class InMemoryAuditLogs : IAuditLogRepository
    {
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>([]);
    }
}
