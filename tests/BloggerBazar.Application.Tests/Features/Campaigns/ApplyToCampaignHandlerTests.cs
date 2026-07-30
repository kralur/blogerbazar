using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class ApplyToCampaignHandlerTests
{
    [Fact]
    public async Task Creates_application_for_approved_blogger_and_published_campaign()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "Campaign", "Description", ["Lifestyle"], null, null, null, null, null);
        campaign.Publish();
        var blogger = BloggerProfile.Create(12, "Madina", "Ташкент", ["Lifestyle"]);
        blogger.Approve();
        var applications = new InMemoryApplicationRepository();
        var handler = new ApplyToCampaignHandler(new InMemoryCampaignRepository(campaign), new InMemoryBloggerRepository(blogger), new InMemoryBusinessRepository(), applications, new SpyUnitOfWork());

        var result = await handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, "Готова к интеграции"), CancellationToken.None);

        Assert.Equal(campaign.Id, result.CampaignId);
        Assert.Equal(blogger.Id, result.BloggerId);
        Assert.Single(applications.Applications);
    }

    [Fact]
    public async Task Rejects_duplicate_campaign_application()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "Campaign", "Description", ["Lifestyle"], null, null, null, null, null);
        campaign.Publish();
        var blogger = BloggerProfile.Create(12, "Madina", "Ташкент", ["Lifestyle"]);
        blogger.Approve();
        var applications = new InMemoryApplicationRepository { Existing = true };
        var handler = new ApplyToCampaignHandler(new InMemoryCampaignRepository(campaign), new InMemoryBloggerRepository(blogger), new InMemoryBusinessRepository(), applications, new SpyUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, null), CancellationToken.None));

        Assert.Contains("already applied", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_application_to_a_campaign_owned_by_the_same_user()
    {
        var business = BusinessProfile.Create(12, "Own business", null);
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["Lifestyle"], null, null, null, null, null);
        campaign.Publish();
        var blogger = BloggerProfile.Create(12, "Madina", "Tashkent", ["Lifestyle"]);
        blogger.Approve();
        var handler = new ApplyToCampaignHandler(
            new InMemoryCampaignRepository(campaign),
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

        public Task<bool> ExistsAsync(Guid campaignId, Guid bloggerId, CancellationToken cancellationToken) => Task.FromResult(Existing);

        public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CampaignApplication?>(null);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
