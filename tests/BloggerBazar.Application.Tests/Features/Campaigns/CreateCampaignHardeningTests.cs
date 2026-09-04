using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CreateCampaignHardeningTests
{
    [Fact]
    public void Validation_rejects_empty_or_whitespace_required_values()
    {
        AssertInvalid(Create(title: ""));
        AssertInvalid(Create(title: "   "));
        AssertInvalid(Create(description: ""));
        AssertInvalid(Create(description: "   "));
        AssertInvalid(Create(categories: ["   "]));
        AssertInvalid(Create(requirements: ["   "]));
    }

    [Fact]
    public void Validation_rejects_invalid_budget_boundaries()
    {
        AssertInvalid(Create(budgetFrom: null, budgetTo: -1));
        AssertInvalid(Create(budgetFrom: -1, budgetTo: null));
        AssertInvalid(Create(budgetFrom: 200, budgetTo: 100));
    }

    [Fact]
    public void Validation_accepts_existing_valid_budget_combinations_and_optional_city()
    {
        AssertValid(Create(city: null, budgetFrom: null, budgetTo: null));
        AssertValid(Create(city: null, budgetFrom: 0, budgetTo: null));
        AssertValid(Create(city: null, budgetFrom: null, budgetTo: 0));
        AssertValid(Create(city: null, budgetFrom: 0, budgetTo: 0));
        AssertValid(Create(city: "tashkent", budgetFrom: 100, budgetTo: 200));
    }

    [Fact]
    public async Task Whitespace_only_city_is_normalized_to_null_without_changing_draft_creation()
    {
        var campaign = await CreateWithHandlerAsync(Create(city: "   ", publishImmediately: false));

        Assert.Null(campaign.City);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
    }

    [Fact]
    public async Task Valid_campaign_can_still_be_published_with_a_null_city()
    {
        var campaign = await CreateWithHandlerAsync(Create(city: null, publishImmediately: true));

        Assert.Null(campaign.City);
        Assert.Equal(CampaignStatus.Published, campaign.Status);
    }

    private static void AssertInvalid(CreateCampaignCommand command) =>
        Assert.False(new CreateCampaignValidator().Validate(command).IsValid);

    private static void AssertValid(CreateCampaignCommand command) =>
        Assert.True(new CreateCampaignValidator().Validate(command).IsValid);

    private static CreateCampaignCommand Create(
        string title = "Campaign",
        string description = "Description",
        string? city = "tashkent",
        IReadOnlyCollection<string>? categories = null,
        IReadOnlyCollection<string>? requirements = null,
        int? budgetFrom = 100,
        int? budgetTo = 200,
        bool publishImmediately = true) =>
        new(10, title, description, city, categories ?? ["beauty"], requirements ?? ["Reels"], budgetFrom, budgetTo, null, publishImmediately);

    private static async Task<Campaign> CreateWithHandlerAsync(CreateCampaignCommand command)
    {
        var campaigns = new InMemoryCampaignRepository();
        var business = BusinessProfile.Create(command.TelegramUserId, "Business", "tashkent");
        var handler = new CreateCampaignHandler(new InMemoryBusinessRepository(business), campaigns, new UnitOfWork());

        await handler.Handle(command, CancellationToken.None);

        return Assert.Single(campaigns.Items);
    }

    private sealed class InMemoryBusinessRepository(params BusinessProfile[] initial) : IBusinessProfileRepository
    {
        private readonly List<BusinessProfile> profiles = [.. initial];

        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) { profiles.Add(profile); return Task.CompletedTask; }
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profiles.SingleOrDefault(profile => profile.Id == id));
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profiles.SingleOrDefault(profile => profile.TelegramUserId == telegramUserId));
    }

    private sealed class InMemoryCampaignRepository : ICampaignRepository
    {
        public List<Campaign> Items { get; } = [];

        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken) { Items.Add(campaign); return Task.CompletedTask; }
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(campaign => campaign.Id == id));
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(campaign => campaign.Id == id && campaign.BusinessId == businessId));
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
