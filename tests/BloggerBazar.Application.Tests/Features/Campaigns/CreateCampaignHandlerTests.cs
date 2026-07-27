using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CreateCampaignHandlerTests
{
    [Fact]
    public async Task Creates_published_campaign_for_existing_business()
    {
        var business = BusinessProfile.Create(123, "Lumi Beauty", "Ташкент");
        var businesses = new InMemoryBusinessRepository(business);
        var campaigns = new InMemoryCampaignRepository();
        var unitOfWork = new SpyUnitOfWork();
        var handler = new CreateCampaignHandler(businesses, campaigns, unitOfWork);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal("Lumi Beauty", result.BusinessName);
        Assert.Equal(1, result.Status);
        Assert.Single(campaigns.Campaigns);
        Assert.Equal(1, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task Requires_business_profile_before_campaign_creation()
    {
        var handler = new CreateCampaignHandler(new InMemoryBusinessRepository(), new InMemoryCampaignRepository(), new SpyUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Contains("business profile", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CreateCampaignCommand CreateCommand() => new(123, "Skincare launch", "Ищем beauty-блогеров для Reels и Stories.", "Ташкент", ["Красота", "Lifestyle"], 700000, 2400000, true);

    private sealed class InMemoryBusinessRepository(params BusinessProfile[] profiles) : IBusinessProfileRepository
    {
        private readonly List<BusinessProfile> profiles = [.. profiles];

        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken)
        {
            profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(profiles.SingleOrDefault(profile => profile.Id == id));

        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
            Task.FromResult(profiles.SingleOrDefault(profile => profile.TelegramUserId == telegramUserId));
    }

    private sealed class InMemoryCampaignRepository : ICampaignRepository
    {
        public List<Campaign> Campaigns { get; } = [];

        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken)
        {
            Campaigns.Add(campaign);
            return Task.CompletedTask;
        }

        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Campaigns.SingleOrDefault(campaign => campaign.Id == id));

        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int SaveCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCallCount++;
            return Task.FromResult(1);
        }
    }
}
