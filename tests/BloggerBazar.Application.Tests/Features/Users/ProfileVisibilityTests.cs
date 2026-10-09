using System.Reflection;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.Offers;
using BloggerBazar.Application.Features.Users;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Users;

// D50: an owner pauses a role profile; it leaves listings and new offers or applications both ways.
public sealed class ProfileVisibilityTests
{
    [Fact]
    public async Task Owner_hides_and_shows_the_profile_of_a_role()
    {
        var business = Business(12, "Abba");
        var handler = new SetProfileVisibilityHandler(new FakeUsers(User(12, MarketplaceRole.Business)), new FakeBloggers(), new FakeBusinesses(business), new FakeBrandFaces(), new SpyUnitOfWork());

        var hidden = await handler.Handle(new SetProfileVisibilityCommand(12, MarketplaceRole.Business, true), CancellationToken.None);
        Assert.True(hidden.IsHidden);
        Assert.True(business.IsHidden);

        await handler.Handle(new SetProfileVisibilityCommand(12, MarketplaceRole.Business, false), CancellationToken.None);
        Assert.False(business.IsHidden);
    }

    [Fact]
    public async Task Hiding_a_role_without_its_profile_is_not_found_and_touches_no_other_role()
    {
        var blogger = Blogger(12);
        var handler = new SetProfileVisibilityHandler(new FakeUsers(User(12, MarketplaceRole.Blogger)), new FakeBloggers(blogger), new FakeBusinesses(), new FakeBrandFaces(), new SpyUnitOfWork());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new SetProfileVisibilityCommand(12, MarketplaceRole.BrandFace, true), CancellationToken.None));

        Assert.Contains("not found", error.Message);
        Assert.False(blogger.IsHidden);
    }

    [Fact]
    public async Task A_blocked_account_cannot_change_visibility()
    {
        var user = User(12, MarketplaceRole.Business);
        user.SetBlocked(true);
        var handler = new SetProfileVisibilityHandler(new FakeUsers(user), new FakeBloggers(), new FakeBusinesses(Business(12)), new FakeBrandFaces(), new SpyUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new SetProfileVisibilityCommand(12, MarketplaceRole.Business, true), CancellationToken.None));
    }

    [Fact]
    public async Task A_hidden_creator_cannot_apply_and_nobody_applies_to_a_hidden_business()
    {
        var business = Business(99);
        business.Approve();
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, null, null);
        campaign.Publish();
        Set(campaign, nameof(Campaign.Business), business);
        var blogger = Blogger(12);
        blogger.Approve();
        blogger.SetHidden(true);
        var applications = new Applications();
        ApplyToCampaignHandler Handler() => new(new Campaigns(campaign), new FakeUsers(User(12, MarketplaceRole.Blogger), User(99, MarketplaceRole.Business)), new FakeBloggers(blogger), new FakeBusinesses(), applications, new SpyUnitOfWork());

        var hiddenCreator = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => Handler().Handle(new ApplyToCampaignCommand(campaign.Id, 12, null), CancellationToken.None));
        blogger.SetHidden(false);
        business.SetHidden(true);
        var hiddenBusiness = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => Handler().Handle(new ApplyToCampaignCommand(campaign.Id, 12, null), CancellationToken.None));

        Assert.Equal(ProfileVisibilityCodes.ProfileHidden, hiddenCreator.Code);
        Assert.Equal(ProfileVisibilityCodes.BusinessHidden, hiddenBusiness.Code);
        Assert.Empty(applications.Values);
    }

    [Fact]
    public async Task A_hidden_business_sends_no_offers_and_a_hidden_creator_receives_none()
    {
        var business = Business(22);
        var blogger = Blogger(11);
        blogger.Approve();
        var brandFace = BrandFaceProfile.Create(33, "Dilnoza", "tashkent", ["beauty"]);
        var offers = new NoOffers();
        CreateOfferHandler Handler() => new(new FakeUsers(User(22, MarketplaceRole.Business)), new FakeBloggers(blogger), new FakeBusinesses(business), offers, new SpyUnitOfWork(), brandFaces: new FakeBrandFaces(brandFace));

        business.SetHidden(true);
        var fromHidden = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => Handler().Handle(new CreateOfferCommand(22, blogger.Id, OfferFormats.Reels, null, null, "Hi"), CancellationToken.None));
        business.SetHidden(false);
        blogger.SetHidden(true);
        brandFace.SetHidden(true);
        var toHiddenBlogger = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(new CreateOfferCommand(22, blogger.Id, OfferFormats.Reels, null, null, "Hi"), CancellationToken.None));
        var toHiddenBrandFace = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(new CreateOfferCommand(22, null, "photoShoot", null, null, "Hi", brandFace.Id), CancellationToken.None));

        Assert.Equal(ProfileVisibilityCodes.ProfileHidden, fromHidden.Code);
        Assert.Contains("not found", toHiddenBlogger.Message);
        Assert.Contains("not found", toHiddenBrandFace.Message);
        Assert.Empty(offers.Added);
    }

    private sealed class Campaigns(Campaign campaign) : ICampaignRepository
    {
        public Task AddAsync(Campaign value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Campaign?>(id == campaign.Id ? campaign : null);
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult<Campaign?>(null);
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class Applications : ICampaignApplicationRepository
    {
        public List<CampaignApplication> Values { get; } = [];
        public Task AddAsync(CampaignApplication application, CancellationToken cancellationToken) { Values.Add(application); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(Guid campaignId, MarketplaceRole creatorRole, Guid creatorId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CampaignApplication?>(null);
    }

    private sealed class NoOffers : ICollaborationRequestRepository
    {
        public List<CollaborationRequest> Added { get; } = [];
        public Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CollaborationRequest?>(null);
        public Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(CollaborationRequest request, CancellationToken cancellationToken) { Added.Add(request); return Task.CompletedTask; }
    }
}
