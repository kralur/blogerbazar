using System.Reflection;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using BloggerBazar.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace BloggerBazar.Application.Tests.Features.Deals;

public sealed class DealIntegrityTests
{
    [Fact]
    public void Campaign_deal_copies_an_immutable_v1_terms_snapshot()
    {
        var business = Business(1);
        var deadline = DateTime.UtcNow.AddDays(10);
        var campaign = Campaign.Create(
            business.Id,
            "Launch A",
            "Original description",
            ["beauty", "fashion"],
            ["One reel"],
            500_000,
            1_500_000,
            "tashkent",
            deadline);

        var snapshot = CampaignTermsSnapshot.FromCampaign(campaign);
        var deal = Deal.Create(Guid.NewGuid(), Guid.NewGuid(), business.Id, snapshot);

        campaign.Update("Launch B", "Changed description", ["food"], ["One story"], 100_000, 200_000, "samarkand", DateTime.UtcNow.AddDays(20));

        Assert.Equal(CampaignTermsSnapshot.Version, deal.CampaignTermsSnapshotVersion);
        Assert.Equal("Launch A", deal.CampaignTitleSnapshot);
        Assert.Equal("Original description", deal.CampaignDescriptionSnapshot);
        Assert.Equal("tashkent", deal.CampaignCitySnapshot);
        Assert.Equal(["beauty", "fashion"], deal.CampaignCategoriesSnapshot);
        Assert.Equal(["One reel"], deal.CampaignRequirementsSnapshot);
        Assert.Equal(500_000, deal.CampaignBudgetFromSnapshot);
        Assert.Equal(1_500_000, deal.CampaignBudgetToSnapshot);
        Assert.Equal(deadline, deal.CampaignDeadlineSnapshot);
    }

    [Fact]
    public void Campaign_deal_does_not_share_snapshot_collections()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "Campaign", "Description", ["beauty"], ["Reel"], null, null, null, null);
        var snapshot = CampaignTermsSnapshot.FromCampaign(campaign);
        var deal = Deal.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), snapshot);

        campaign.Update("Campaign", "Description", ["fashion"], ["Story"], null, null, null, null);

        Assert.Equal(["beauty"], deal.CampaignCategoriesSnapshot);
        Assert.Equal(["Reel"], deal.CampaignRequirementsSnapshot);
    }

    [Fact]
    public void Legacy_and_direct_deals_have_no_campaign_snapshot()
    {
        var legacyDeal = Deal.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var collaborationDeal = Deal.CreateFromCollaborationRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(legacyDeal.CampaignTermsSnapshotVersion);
        Assert.Null(legacyDeal.CampaignTitleSnapshot);
        Assert.Null(collaborationDeal.CampaignTermsSnapshotVersion);
        Assert.Null(collaborationDeal.CampaignCategoriesSnapshot);
    }

    [Fact]
    public void Invalid_campaign_terms_cannot_be_materialized_as_a_snapshot()
    {
        var invalidCampaign = Campaign.Create(Guid.NewGuid(), "", "Description", ["beauty"], null, null, null, null, null);

        Assert.Throws<ArgumentException>(() => CampaignTermsSnapshot.FromCampaign(invalidCampaign));
    }

    [Fact]
    public void Deal_mapping_and_migration_add_only_nullable_campaign_snapshot_columns()
    {
        var options = new DbContextOptionsBuilder<BloggerBazarDbContext>()
            .UseNpgsql("Host=localhost;Database=bloggerbazar_test;Username=test")
            .Options;
        using var dbContext = new BloggerBazarDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(Deal))!;

        Assert.Equal("smallint", entity.FindProperty(nameof(Deal.CampaignTermsSnapshotVersion))!.GetColumnType());
        Assert.Equal(160, entity.FindProperty(nameof(Deal.CampaignTitleSnapshot))!.GetMaxLength());
        Assert.Equal(3000, entity.FindProperty(nameof(Deal.CampaignDescriptionSnapshot))!.GetMaxLength());
        Assert.Equal("text[]", entity.FindProperty(nameof(Deal.CampaignCategoriesSnapshot))!.GetColumnType());
        Assert.Equal("text[]", entity.FindProperty(nameof(Deal.CampaignRequirementsSnapshot))!.GetColumnType());
        Assert.Equal("timestamp with time zone", entity.FindProperty(nameof(Deal.CampaignDeadlineSnapshot))!.GetColumnType());

        var migrationBuilder = new MigrationBuilder("Npgsql");
        typeof(AddDealCampaignTermsSnapshot).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new AddDealCampaignTermsSnapshot(), [migrationBuilder]);
        var columns = migrationBuilder.Operations.OfType<AddColumnOperation>().ToArray();

        Assert.Equal(9, columns.Length);
        Assert.All(columns, column => Assert.Equal("deals", column.Table));
        Assert.All(columns, column => Assert.True(column.IsNullable));
    }

    [Fact]
    public async Task Legacy_accept_creates_a_v1_snapshot_and_is_idempotent()
    {
        var business = Business(10);
        var blogger = Blogger(11);
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], ["Reel"], 100, 200, "tashkent", DateTime.UtcNow.AddDays(7));
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        Attach(application, nameof(CampaignApplication.Campaign), campaign);
        var deals = new Deals();
        var handler = new AcceptCampaignApplicationHandler(
            new Applications(application),
            new Users(User(business.TelegramUserId, MarketplaceRole.Business)),
            new Businesses(business),
            deals,
            new UnitOfWork(),
            new Bloggers(blogger));

        var first = await handler.Handle(new AcceptCampaignApplicationCommand(application.Id, business.TelegramUserId), CancellationToken.None);
        var second = await handler.Handle(new AcceptCampaignApplicationCommand(application.Id, business.TelegramUserId), CancellationToken.None);

        var deal = Assert.Single(deals.Items);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(CampaignTermsSnapshot.Version, deal.CampaignTermsSnapshotVersion);
        Assert.Equal(campaign.Title, deal.CampaignTitleSnapshot);
        Assert.Equal(campaign.Categories, deal.CampaignCategoriesSnapshot);
    }

    [Theory]
    [InlineData(MarketplaceRole.Blogger)]
    [InlineData(MarketplaceRole.BrandFace)]
    public async Task Legacy_accept_requires_an_active_business_role(MarketplaceRole role)
    {
        var business = Business(20);
        var blogger = Blogger(21);
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, null, null);
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        Attach(application, nameof(CampaignApplication.Campaign), campaign);
        var handler = new AcceptCampaignApplicationHandler(
            new Applications(application),
            new Users(User(business.TelegramUserId, role)),
            new Businesses(business),
            new Deals(),
            new UnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new AcceptCampaignApplicationCommand(application.Id, business.TelegramUserId), CancellationToken.None));
    }

    [Fact]
    public async Task Legacy_accept_denies_blocked_or_deleted_business_accounts()
    {
        var business = Business(30);
        var blogger = Blogger(31);
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, null, null);
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        Attach(application, nameof(CampaignApplication.Campaign), campaign);
        var user = User(business.TelegramUserId, MarketplaceRole.Business);
        user.SetBlocked(true);
        var handler = new AcceptCampaignApplicationHandler(new Applications(application), new Users(user), new Businesses(business), new Deals(), new UnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new AcceptCampaignApplicationCommand(application.Id, business.TelegramUserId), CancellationToken.None));

        user.SetBlocked(false);
        user.SoftDelete(user.TelegramUserId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new AcceptCampaignApplicationCommand(application.Id, business.TelegramUserId), CancellationToken.None));
    }

    [Fact]
    public async Task Legacy_accept_returns_the_persisted_winner_after_a_unique_deal_race()
    {
        var business = Business(35);
        var blogger = Blogger(36);
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, null, null);
        var application = CampaignApplication.Create(campaign.Id, blogger.Id, null);
        Attach(application, nameof(CampaignApplication.Campaign), campaign);
        var deals = new RaceDeals();
        var handler = new AcceptCampaignApplicationHandler(
            new Applications(application),
            new Users(User(business.TelegramUserId, MarketplaceRole.Business)),
            new Businesses(business),
            deals,
            new UniqueConflictUnitOfWork(() => deals.PersistWinner(Deal.Create(application.Id, blogger.Id, business.Id, CampaignTermsSnapshot.FromCampaign(campaign)))));

        var result = await handler.Handle(new AcceptCampaignApplicationCommand(application.Id, business.TelegramUserId), CancellationToken.None);

        var winner = Assert.Single(deals.Items, deal => deal.Id == result.Id);
        Assert.Equal(CampaignTermsSnapshot.Version, winner.CampaignTermsSnapshotVersion);
    }

    [Fact]
    public async Task Collaboration_deal_requires_the_selected_participant_role_and_has_no_snapshot()
    {
        var blogger = Blogger(40);
        var business = Business(41);
        var request = CollaborationRequest.Create(blogger.Id, business.Id, "Let's collaborate");
        Attach(request, nameof(CollaborationRequest.Blogger), blogger);
        Attach(request, nameof(CollaborationRequest.Business), business);
        var deals = new Deals();
        var handler = new CreateDealFromCollaborationRequestHandler(
            new Requests(request),
            deals,
            new Users(User(blogger.TelegramUserId, MarketplaceRole.Blogger)),
            new Bloggers(blogger),
            new Businesses(business),
            new UnitOfWork());

        var result = await handler.Handle(new CreateDealFromCollaborationRequestCommand(request.Id, blogger.TelegramUserId), CancellationToken.None);

        var deal = Assert.Single(deals.Items);
        Assert.Equal(deal.Id, result.Id);
        Assert.Null(deal.CampaignTermsSnapshotVersion);
        Assert.Null(deal.CampaignTitleSnapshot);
    }

    [Fact]
    public async Task Collaboration_deal_denies_an_actor_with_a_nonparticipant_selected_role()
    {
        var blogger = Blogger(50);
        var business = Business(51);
        var request = CollaborationRequest.Create(blogger.Id, business.Id, "Let's collaborate");
        Attach(request, nameof(CollaborationRequest.Blogger), blogger);
        Attach(request, nameof(CollaborationRequest.Business), business);
        var handler = new CreateDealFromCollaborationRequestHandler(
            new Requests(request),
            new Deals(),
            new Users(User(blogger.TelegramUserId, MarketplaceRole.Business)),
            new Bloggers(blogger),
            new Businesses(business),
            new UnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new CreateDealFromCollaborationRequestCommand(request.Id, blogger.TelegramUserId), CancellationToken.None));
    }

    private static PlatformUser User(long telegramUserId, MarketplaceRole role)
    {
        var user = PlatformUser.Create(telegramUserId, "User", null);
        user.SelectMarketplaceRole(role);
        return user;
    }

    private static BloggerProfile Blogger(long telegramUserId) => BloggerProfile.Create(telegramUserId, "Blogger", "tashkent", ["beauty"]);

    private static BusinessProfile Business(long telegramUserId) => BusinessProfile.Create(telegramUserId, "Business", "tashkent");

    private static void Attach<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);

    private sealed class Users(params PlatformUser[] users) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([]);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Bloggers(params BloggerProfile[] bloggers) : IBloggerProfileRepository
    {
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(bloggers.SingleOrDefault(blogger => blogger.Id == id));
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(bloggers.SingleOrDefault(blogger => blogger.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Businesses(params BusinessProfile[] businesses) : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(businesses.SingleOrDefault(business => business.Id == id));
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(businesses.SingleOrDefault(business => business.TelegramUserId == telegramUserId));
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Applications(CampaignApplication application) : ICampaignApplicationRepository
    {
        public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CampaignApplication?>(id == application.Id ? application : null);
        public Task<bool> ExistsAsync(Guid campaignId, Guid bloggerId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(CampaignApplication value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Requests(CollaborationRequest request) : ICollaborationRequestRepository
    {
        public Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CollaborationRequest?>(id == request.Id ? request : null);
        public Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(CollaborationRequest value, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Deals : IDealRepository
    {
        public List<Deal> Items { get; } = [];
        public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(deal => deal.Id == id));
        public Task<Deal?> GetByCampaignApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(deal => deal.CampaignApplicationId == applicationId));
        public Task<Deal?> GetByCollaborationRequestIdAsync(Guid requestId, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(deal => deal.CollaborationRequestId == requestId));
        public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(Items.Any(deal => deal.CampaignApplicationId == campaignApplicationId));
        public Task AddAsync(Deal deal, CancellationToken cancellationToken) { Items.Add(deal); return Task.CompletedTask; }
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class UniqueConflictUnitOfWork(Action persistWinner) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            persistWinner();
            return Task.FromResult(false);
        }
    }

    private sealed class RaceDeals : IDealRepository
    {
        public List<Deal> Items { get; } = [];
        public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(deal => deal.Id == id));
        public Task<Deal?> GetByCampaignApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(deal => deal.CampaignApplicationId == applicationId));
        public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(Items.Any(deal => deal.CampaignApplicationId == campaignApplicationId));
        public Task AddAsync(Deal deal, CancellationToken cancellationToken) => Task.CompletedTask;
        public void PersistWinner(Deal deal) => Items.Add(deal);
    }
}
