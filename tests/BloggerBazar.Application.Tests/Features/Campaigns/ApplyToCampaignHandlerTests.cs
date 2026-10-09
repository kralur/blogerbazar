using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using System.Reflection;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class ApplyToCampaignHandlerTests
{
    [Fact]
    public async Task Creates_application_for_approved_blogger_and_published_campaign()
    {
        var business = ApprovedBusiness(99, "Business");
        var campaign = PublishedCampaign(business);
        var blogger = BloggerProfile.Create(12, "Madina", "Ташкент", ["Lifestyle"]);
        blogger.Approve();
        var applications = new InMemoryApplicationRepository();
        var handler = new ApplyToCampaignHandler(new InMemoryCampaignRepository(campaign), new InMemoryPlatformUserRepository(BloggerUser(12), User(99)), new InMemoryBloggerRepository(blogger), new InMemoryBusinessRepository(), applications, new SpyUnitOfWork());

        var result = await handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, "Готова к интеграции"), CancellationToken.None);

        Assert.Equal(campaign.Id, result.CampaignId);
        Assert.Equal(blogger.Id, result.BloggerId);
        Assert.Single(applications.Applications);
    }

    [Fact]
    public async Task Notifies_the_business_in_both_languages_with_a_button_to_the_inbox()
    {
        var business = ApprovedBusiness(99, "Business");
        var campaign = PublishedCampaign(business);
        var blogger = BloggerProfile.Create(12, "Madina", "Ташкент", ["Lifestyle"]);
        blogger.Approve();
        var bot = new SpyBotClient();
        var handler = new ApplyToCampaignHandler(new InMemoryCampaignRepository(campaign), new InMemoryPlatformUserRepository(BloggerUser(12), User(99)), new InMemoryBloggerRepository(blogger), new InMemoryBusinessRepository(), new InMemoryApplicationRepository(), new SpyUnitOfWork(), botClient: bot);

        await handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, null), CancellationToken.None);

        Assert.Equal(99L, Assert.Single(bot.NotifiedChats));
        var text = Assert.Single(bot.Texts);
        Assert.Contains("Новый отклик от Madina", text);
        Assert.Contains("kampaniyasiga javob yubordi", text);
        Assert.Equal($"/my-campaign-applications/{campaign.Id}", Assert.Single(bot.Routes));
    }

    [Fact]
    public async Task Rejects_application_after_the_campaign_deadline()
    {
        var business = ApprovedBusiness(99, "Business");
        var campaign = PublishedCampaign(business, DateTime.UtcNow.Date.AddDays(-1));
        var blogger = BloggerProfile.Create(12, "Madina", "Ташкент", ["Lifestyle"]);
        blogger.Approve();
        var applications = new InMemoryApplicationRepository();
        var handler = new ApplyToCampaignHandler(new InMemoryCampaignRepository(campaign), new InMemoryPlatformUserRepository(BloggerUser(12), User(99)), new InMemoryBloggerRepository(blogger), new InMemoryBusinessRepository(), applications, new SpyUnitOfWork());

        var exception = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, null), CancellationToken.None));

        Assert.Equal("campaign_expired", exception.Code);
        Assert.Empty(applications.Applications);
    }

    [Fact]
    public async Task Rejects_duplicate_campaign_application()
    {
        var business = ApprovedBusiness(99, "Business");
        var campaign = PublishedCampaign(business);
        var blogger = BloggerProfile.Create(12, "Madina", "Ташкент", ["Lifestyle"]);
        blogger.Approve();
        var applications = new InMemoryApplicationRepository { Existing = true };
        var handler = new ApplyToCampaignHandler(new InMemoryCampaignRepository(campaign), new InMemoryPlatformUserRepository(BloggerUser(12), User(99)), new InMemoryBloggerRepository(blogger), new InMemoryBusinessRepository(), applications, new SpyUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, null), CancellationToken.None));

        Assert.Contains("already applied", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_application_to_a_campaign_owned_by_the_same_user()
    {
        var business = ApprovedBusiness(12, "Own business");
        var campaign = PublishedCampaign(business);
        var blogger = BloggerProfile.Create(12, "Madina", "Tashkent", ["Lifestyle"]);
        blogger.Approve();
        var handler = new ApplyToCampaignHandler(
            new InMemoryCampaignRepository(campaign),
            new InMemoryPlatformUserRepository(BloggerUser(12)),
            new InMemoryBloggerRepository(blogger),
            new InMemoryBusinessRepository(business),
            new InMemoryApplicationRepository(),
            new SpyUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, null), CancellationToken.None));

        Assert.Contains("own campaign", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InMemoryCampaignRepository(Campaign campaign) : ICampaignRepository
    {
        public Task AddAsync(Campaign value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Campaign?>(id == campaign.Id ? campaign : null);
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult<Campaign?>(id == campaign.Id && campaign.BusinessId == businessId ? campaign : null);
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile blogger) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == blogger.Id ? blogger : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(telegramUserId == blogger.TelegramUserId ? blogger : null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class InMemoryBusinessRepository(BusinessProfile? business = null) : IBusinessProfileRepository
    {
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(business?.Id == id ? business : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(business?.TelegramUserId == telegramUserId ? business : null);
    }

    private sealed class InMemoryApplicationRepository : ICampaignApplicationRepository
    {
        public bool Existing { get; init; }
        public List<CampaignApplication> Applications { get; } = [];
        public Task AddAsync(CampaignApplication application, CancellationToken cancellationToken)
        {
            Applications.Add(application);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid campaignId, MarketplaceRole creatorRole, Guid bloggerId, CancellationToken cancellationToken) => Task.FromResult(Existing);

        public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CampaignApplication?>(null);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private static BusinessProfile ApprovedBusiness(long telegramUserId, string name)
    {
        var business = BusinessProfile.Create(telegramUserId, name, "tashkent");
        business.Approve();
        return business;
    }

    private static Campaign PublishedCampaign(BusinessProfile business, DateTime? deadline = null)
    {
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["Lifestyle"], null, null, null, null, deadline);
        campaign.Publish();
        typeof(Campaign).GetProperty(nameof(Campaign.Business), BindingFlags.Instance | BindingFlags.Public)!.SetValue(campaign, business);
        return campaign;
    }

    private static PlatformUser BloggerUser(long telegramUserId)
    {
        var user = User(telegramUserId);
        user.SelectMarketplaceRole(MarketplaceRole.Blogger);
        return user;
    }

    private static PlatformUser User(long telegramUserId) => PlatformUser.Create(telegramUserId, "User", null);

    private sealed class InMemoryPlatformUserRepository(params PlatformUser[] users) : IPlatformUserRepository
    {
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Count(user => !user.IsDeleted));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users.Where(user => !user.IsDeleted).Take(take).ToArray());
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
    }
}
