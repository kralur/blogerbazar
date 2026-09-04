using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Api.Contracts.Campaigns;
using BloggerBazar.Api.Controllers;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Authentication;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CampaignManagementMutationTests
{
    [Fact]
    public async Task Private_mutation_routes_require_telegram_authorization_before_dispatching()
    {
        var controller = new CampaignsController(null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var request = new UpdateCampaignRequest("Title", "Description", "tashkent", ["beauty"], null, null, null, null);

        await Assert.ThrowsAsync<AuthenticationException>(() => controller.Update(Guid.NewGuid(), request, CancellationToken.None));
        await Assert.ThrowsAsync<AuthenticationException>(() => controller.Close(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Owner_can_edit_campaign_without_changing_its_lifecycle_or_creation_data()
    {
        var business = Business(10);
        var campaign = CampaignFor(business, CampaignStatus.Published);
        var createdAt = campaign.CreatedAtUtc;
        var updatedAt = campaign.UpdatedAtUtc;
        var cache = new RecordingCache();
        var handler = new UpdateCampaignHandler(new CampaignRepository(campaign), new UserRepository(User(10)), new BusinessRepository(business), new UnitOfWork(), cache);

        var result = await handler.Handle(new UpdateCampaignCommand(campaign.Id, 10, " Updated ", " New description ", "  tashkent  ", [" beauty "], [" Reels "], 500_000, 1_500_000, new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        Assert.Equal("Updated", campaign.Title);
        Assert.Equal("New description", campaign.Description);
        Assert.Equal("tashkent", campaign.City);
        Assert.Equal(["beauty"], campaign.Categories);
        Assert.Equal(["Reels"], campaign.Requirements);
        Assert.Equal(500_000, campaign.BudgetFrom);
        Assert.Equal(1_500_000, campaign.BudgetTo);
        Assert.Equal(CampaignStatus.Published, campaign.Status);
        Assert.Equal(createdAt, campaign.CreatedAtUtc);
        Assert.True(campaign.UpdatedAtUtc >= updatedAt);
        Assert.Equal((int)CampaignStatus.Published, result.Status);
        Assert.Equal(["campaigns"], cache.RotatedCatalogs);
    }

    [Fact]
    public async Task Foreign_campaign_is_not_found_for_an_active_business_owner()
    {
        var owner = Business(10);
        var other = Business(20);
        var campaign = CampaignFor(owner, CampaignStatus.Draft);
        var handler = new UpdateCampaignHandler(new CampaignRepository(campaign), new UserRepository(User(20)), new BusinessRepository(other), new UnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Update(campaign.Id, 20), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Only_an_active_business_role_can_edit_or_close_campaigns()
    {
        var business = Business(10);
        var campaign = CampaignFor(business, CampaignStatus.Draft);
        var blogger = PlatformUser.Create(10, "Blogger", null);
        blogger.SelectMarketplaceRole(MarketplaceRole.Blogger);
        var users = new UserRepository(blogger);
        var campaigns = new CampaignRepository(campaign);
        var businesses = new BusinessRepository(business);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new UpdateCampaignHandler(campaigns, users, businesses, new UnitOfWork()).Handle(Update(campaign.Id, 10), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new CloseCampaignHandler(campaigns, users, businesses, new UnitOfWork()).Handle(new CloseCampaignCommand(campaign.Id, 10), CancellationToken.None));
    }

    [Fact]
    public async Task Blocked_deleted_or_profileless_businesses_cannot_mutate_campaigns()
    {
        var business = Business(10);
        var campaign = CampaignFor(business, CampaignStatus.Draft);
        var blocked = User(10);
        blocked.SetBlocked(true);
        var deleted = User(10);
        deleted.SoftDelete(10);
        var deletedBusiness = Business(10);
        deletedBusiness.SoftDelete();

        await AssertCannotMutateAsync(blocked, new BusinessRepository(business), campaign);
        await AssertCannotMutateAsync(deleted, new BusinessRepository(business), campaign);
        await AssertCannotMutateAsync(User(10), new BusinessRepository(deletedBusiness), campaign);
        await AssertCannotMutateAsync(User(10), new BusinessRepository(), campaign);
    }

    [Fact]
    public async Task Archived_campaign_cannot_be_edited_and_does_not_invalidate_cache()
    {
        var business = Business(10);
        var campaign = CampaignFor(business, CampaignStatus.Archived);
        var cache = new RecordingCache();
        var handler = new UpdateCampaignHandler(new CampaignRepository(campaign), new UserRepository(User(10)), new BusinessRepository(business), new UnitOfWork(), cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Update(campaign.Id, 10), CancellationToken.None));

        Assert.Empty(cache.RotatedCatalogs);
    }

    [Fact]
    public async Task Close_is_idempotent_and_keeps_the_campaign_for_owner_management()
    {
        var business = Business(10);
        var campaign = CampaignFor(business, CampaignStatus.Published);
        var cache = new RecordingCache();
        var handler = new CloseCampaignHandler(new CampaignRepository(campaign), new UserRepository(User(10)), new BusinessRepository(business), new UnitOfWork(), cache);

        await handler.Handle(new CloseCampaignCommand(campaign.Id, 10), CancellationToken.None);
        var repeated = await handler.Handle(new CloseCampaignCommand(campaign.Id, 10), CancellationToken.None);

        Assert.Equal(CampaignStatus.Archived, campaign.Status);
        Assert.Equal((int)CampaignStatus.Archived, repeated.Status);
        Assert.Equal(["campaigns"], cache.RotatedCatalogs);
    }

    [Theory]
    [InlineData("   ", "Description", 0, 100)]
    [InlineData("Title", "   ", 0, 100)]
    [InlineData("Title", "Description", -1, 100)]
    [InlineData("Title", "Description", 100, 0)]
    public void Edit_validation_matches_create_boundaries_for_required_text_and_budgets(string title, string description, int budgetFrom, int budgetTo)
    {
        var result = new UpdateCampaignValidator().Validate(new UpdateCampaignCommand(Guid.NewGuid(), 10, title, description, "tashkent", ["beauty"], ["Reels"], budgetFrom, budgetTo, null));

        Assert.False(result.IsValid);
    }

    private static UpdateCampaignCommand Update(Guid campaignId, long telegramUserId) =>
        new(campaignId, telegramUserId, "Title", "Description", "tashkent", ["beauty"], null, 0, 100, null);

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

    private static Campaign CampaignFor(BusinessProfile business, CampaignStatus status)
    {
        var campaign = Campaign.Create(business.Id, "Title", "Description", ["beauty"], null, 100, 200, "tashkent", null);
        campaign.SetStatus(status);
        return campaign;
    }

    private static async Task AssertCannotMutateAsync(PlatformUser user, IBusinessProfileRepository businesses, Campaign campaign)
    {
        var campaigns = new CampaignRepository(campaign);
        var users = new UserRepository(user);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new UpdateCampaignHandler(campaigns, users, businesses, new UnitOfWork()).Handle(Update(campaign.Id, user.TelegramUserId), CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new CloseCampaignHandler(campaigns, users, businesses, new UnitOfWork()).Handle(new CloseCampaignCommand(campaign.Id, user.TelegramUserId), CancellationToken.None));
    }

    private sealed class CampaignRepository(params Campaign[] initial) : ICampaignRepository
    {
        private readonly List<Campaign> campaigns = [.. initial];
        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken) { campaigns.Add(campaign); return Task.CompletedTask; }
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(campaigns.SingleOrDefault(campaign => campaign.Id == id));
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult(campaigns.SingleOrDefault(campaign => campaign.Id == id && campaign.BusinessId == businessId));
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

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
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
