using System.Runtime.CompilerServices;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Payments;

public sealed class GetUnlockedContactHandlerTests
{
    [Fact]
    public async Task Business_in_a_deal_sees_the_blogger_contact()
    {
        var blogger = BloggerWithContact();
        var business = Business(303);
        var handler = Handler(blogger, business, User(303, MarketplaceRole.Business), CampaignDeal(blogger, business));

        var result = await handler.Handle(new GetUnlockedContactQuery(ContactTargetType.Blogger, blogger.Id, 303), CancellationToken.None);

        Assert.Equal("+998901234567", result.Phone);
        Assert.Equal("madina@example.com", result.Email);
        Assert.Equal("@madina", result.Telegram);
    }

    [Fact]
    public async Task Blogger_in_a_deal_sees_the_business_contact()
    {
        var blogger = BloggerWithContact();
        var business = Business(303);
        business.Update("Lumi", "@lumi", "tashkent", null, "https://lumi.example", null, "+998911112233", "hello@lumi.example");
        var handler = Handler(blogger, business, User(202, MarketplaceRole.Blogger), CampaignDeal(blogger, business));

        var result = await handler.Handle(new GetUnlockedContactQuery(ContactTargetType.Business, business.Id, 202), CancellationToken.None);

        Assert.Equal("+998911112233", result.Phone);
        Assert.Equal("https://lumi.example", result.WebsiteUrl);
    }

    [Fact]
    public async Task Contact_is_hidden_without_a_shared_deal()
    {
        var blogger = BloggerWithContact();
        var business = Business(303);
        var handler = Handler(blogger, business, User(303, MarketplaceRole.Business));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetUnlockedContactQuery(ContactTargetType.Blogger, blogger.Id, 303), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(MarketplaceRole.Blogger)]
    [InlineData(MarketplaceRole.BrandFace)]
    public async Task Deal_access_follows_the_selected_role(MarketplaceRole role)
    {
        var blogger = BloggerWithContact();
        var business = Business(303);
        var handler = Handler(blogger, business, User(303, role), CampaignDeal(blogger, business));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetUnlockedContactQuery(ContactTargetType.Blogger, blogger.Id, 303), CancellationToken.None));
    }

    [Fact]
    public async Task Previously_paid_unlock_keeps_access()
    {
        var blogger = BloggerWithContact();
        var handler = new GetUnlockedContactHandler(
            new FakeBloggers(blogger),
            new FakeBusinesses(),
            new FakeUsers(User(404, MarketplaceRole.BrandFace)),
            new SharedDeals(),
            new Unlocks((404, ContactTargetType.Blogger, blogger.Id)));

        var result = await handler.Handle(new GetUnlockedContactQuery(ContactTargetType.Blogger, blogger.Id, 404), CancellationToken.None);

        Assert.Equal("+998901234567", result.Phone);
    }

    [Fact]
    public async Task Missing_profile_is_not_found()
    {
        var handler = Handler(BloggerWithContact(), Business(303), User(303, MarketplaceRole.Business));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetUnlockedContactQuery(ContactTargetType.Blogger, Guid.NewGuid(), 303), CancellationToken.None));
    }

    [Fact]
    public async Task Deal_contact_returns_the_counterparty_of_the_selected_side()
    {
        var blogger = BloggerWithContact();
        var business = Business(303);
        business.Update("Lumi", "@lumi", "tashkent", null, null, null, "+998911112233", null);
        var deal = CampaignDeal(blogger, business);
        var deals = new FakeDeals(deal);

        var asBusiness = await new GetMyDealContactHandler(deals, new FakeUsers(User(303, MarketplaceRole.Business)), new FakeBloggers(blogger), new FakeBusinesses(business))
            .Handle(new GetMyDealContactQuery(deal.Id, 303), CancellationToken.None);
        var asBlogger = await new GetMyDealContactHandler(deals, new FakeUsers(User(202, MarketplaceRole.Blogger)), new FakeBloggers(blogger), new FakeBusinesses(business))
            .Handle(new GetMyDealContactQuery(deal.Id, 202), CancellationToken.None);

        Assert.Equal("@madina", asBusiness.Telegram);
        Assert.Equal("@lumi", asBlogger.Telegram);
    }

    [Fact]
    public async Task Deal_contact_of_a_foreign_deal_is_not_found()
    {
        var blogger = BloggerWithContact();
        var business = Business(303);
        var outsider = Business(505);
        var deal = CampaignDeal(blogger, business);
        var handler = new GetMyDealContactHandler(new FakeDeals(deal), new FakeUsers(User(505, MarketplaceRole.Business)), new FakeBloggers(blogger), new FakeBusinesses(business, outsider));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new GetMyDealContactQuery(deal.Id, 505), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GetUnlockedContactHandler Handler(BloggerProfile blogger, BusinessProfile business, PlatformUser viewer, params Deal[] deals) =>
        new(new FakeBloggers(blogger), new FakeBusinesses(business), new FakeUsers(viewer), new SharedDeals(deals), new Unlocks());

    private static BloggerProfile BloggerWithContact()
    {
        var profile = BloggerProfile.Create(202, "Madina", "Tashkent", ["Lifestyle"]);
        profile.UpdatePublicProfile("Madina", null, "@madina", "Tashkent", ["Lifestyle"], null, null, "+998901234567", "madina@example.com", 1000, null, null, null, null, null, null, false);
        return profile;
    }

    private sealed class SharedDeals(params Deal[] deals) : IDealRepository
    {
        public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(deals.SingleOrDefault(deal => deal.Id == id));
        public Task<bool> ExistsBetweenAsync(Guid bloggerId, Guid businessId, CancellationToken cancellationToken) =>
            Task.FromResult(deals.Any(deal => deal.BloggerId == bloggerId && deal.BusinessId == businessId));
        public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(Deal deal, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Unlocks(params (long Viewer, ContactTargetType Type, Guid Target)[] unlocked) : IContactUnlockRepository
    {
        public Task<ContactUnlock?> GetAsync(long viewerTelegramUserId, ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
            Task.FromResult(unlocked.Contains((viewerTelegramUserId, targetType, targetId))
                ? (ContactUnlock)RuntimeHelpers.GetUninitializedObject(typeof(ContactUnlock))
                : null);

        public Task AddAsync(ContactUnlock contactUnlock, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
