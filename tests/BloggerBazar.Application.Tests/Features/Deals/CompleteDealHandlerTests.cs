using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Deals;

public sealed class CompleteDealHandlerTests
{
    [Theory]
    [InlineData(MarketplaceRole.Blogger)]
    [InlineData(MarketplaceRole.Business)]
    public async Task Completes_an_active_deal_for_the_selected_role_participant(MarketplaceRole role)
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var deal = CampaignDeal(blogger, business);
        var actor = role == MarketplaceRole.Blogger ? blogger.TelegramUserId : business.TelegramUserId;
        var bot = new SpyBotClient();
        var handler = Handler(deal, [User(actor, role)], blogger, business, bot);

        var result = await handler.Handle(new CompleteDealCommand(deal.Id, actor), CancellationToken.None);

        Assert.Equal((int)DealStatus.Completed, result.Status);
        Assert.NotNull(result.CompletedAtUtc);
        var counterparty = role == MarketplaceRole.Blogger ? business.TelegramUserId : blogger.TelegramUserId;
        Assert.Equal([counterparty], bot.NotifiedChats);
    }

    [Fact]
    public async Task Repeated_complete_returns_the_persisted_deal_without_changing_it()
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var deal = CampaignDeal(blogger, business);
        var bot = new SpyBotClient();
        var deals = new FakeDeals(deal);
        var handler = new CompleteDealHandler(deals, new FakeUsers(User(11, MarketplaceRole.Blogger)), new FakeBloggers(blogger), new FakeBusinesses(business), bot);

        var first = await handler.Handle(new CompleteDealCommand(deal.Id, 11), CancellationToken.None);
        var second = await handler.Handle(new CompleteDealCommand(deal.Id, 11), CancellationToken.None);

        Assert.Equal(first.CompletedAtUtc, second.CompletedAtUtc);
        Assert.Equal((int)DealStatus.Completed, second.Status);
        Assert.Equal(1, deals.CompletionAttempts);
        Assert.Single(bot.NotifiedChats);
    }

    [Fact]
    public async Task Losing_a_concurrent_completion_returns_the_winner_without_a_second_notification()
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var deal = CampaignDeal(blogger, business);
        DateTime? winnerCompletedAt = null;
        var deals = new FakeDeals(deal)
        {
            BeforeCompletion = candidate =>
            {
                candidate.Complete();
                winnerCompletedAt = candidate.CompletedAtUtc;
            }
        };
        var bot = new SpyBotClient();
        var handler = new CompleteDealHandler(deals, new FakeUsers(User(22, MarketplaceRole.Business)), new FakeBloggers(blogger), new FakeBusinesses(business), bot);

        var result = await handler.Handle(new CompleteDealCommand(deal.Id, 22), CancellationToken.None);

        Assert.Equal((int)DealStatus.Completed, result.Status);
        Assert.Equal(winnerCompletedAt, result.CompletedAtUtc);
        Assert.Empty(bot.NotifiedChats);
    }

    [Fact]
    public async Task Foreign_deal_is_reported_as_missing()
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var outsider = Blogger(33);
        var deal = CampaignDeal(blogger, business);
        var handler = Handler(deal, [User(33, MarketplaceRole.Blogger)], outsider, business);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CompleteDealCommand(deal.Id, 33), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DealStatus.Active, deal.Status);
    }

    [Fact]
    public async Task Selected_business_role_cannot_complete_a_deal_of_the_same_users_blogger_profile()
    {
        var blogger = Blogger(11);
        var ownBusiness = Business(11);
        var counterparty = Business(22);
        var deal = CampaignDeal(blogger, counterparty);
        var handler = HandlerWithBusinesses(deal, [User(11, MarketplaceRole.Business)], blogger, ownBusiness, counterparty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CompleteDealCommand(deal.Id, 11), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DealStatus.Active, deal.Status);
    }

    [Theory]
    [InlineData(MarketplaceRole.BrandFace)]
    [InlineData(null)]
    public async Task Unsupported_or_missing_selected_role_is_reported_as_missing(MarketplaceRole? role)
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var deal = CampaignDeal(blogger, business);
        var handler = Handler(deal, [User(11, role)], blogger, business);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CompleteDealCommand(deal.Id, 11), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Blocked_participant_is_reported_as_missing()
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var deal = CampaignDeal(blogger, business);
        var user = User(11, MarketplaceRole.Blogger);
        user.SetBlocked(true);
        var handler = Handler(deal, [user], blogger, business);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CompleteDealCommand(deal.Id, 11), CancellationToken.None));

        Assert.Equal(DealStatus.Active, deal.Status);
    }

    [Fact]
    public async Task Cancelled_deal_cannot_be_completed()
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var deal = CampaignDeal(blogger, business);
        Set(deal, nameof(Deal.Status), DealStatus.Cancelled);
        var handler = Handler(deal, [User(11, MarketplaceRole.Blogger)], blogger, business);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CompleteDealCommand(deal.Id, 11), CancellationToken.None));

        Assert.DoesNotContain("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DealStatus.Cancelled, deal.Status);
    }

    private static CompleteDealHandler Handler(Deal deal, PlatformUser[] users, BloggerProfile blogger, BusinessProfile business, SpyBotClient? bot = null) =>
        new(new FakeDeals(deal), new FakeUsers(users), new FakeBloggers(blogger), new FakeBusinesses(business), bot);

    private static CompleteDealHandler HandlerWithBusinesses(Deal deal, PlatformUser[] users, BloggerProfile blogger, BusinessProfile ownBusiness, BusinessProfile otherBusiness) =>
        new(new FakeDeals(deal), new FakeUsers(users), new FakeBloggers(blogger), new FakeBusinesses(ownBusiness, otherBusiness));
}
