using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Deals;

public sealed class ShareDealContactHandlerTests
{
    [Fact]
    public async Task Sends_the_partner_contact_card_to_the_requester_own_chat()
    {
        var blogger = Blogger(11);
        var business = Business(22, "Lumi Beauty");
        Set(business, "Phone", "+998 90 123 45 67");
        var deal = CampaignDeal(blogger, business);
        var bot = new SpyBotClient();

        await Handler(deal, MarketplaceRole.Blogger, 11, blogger, business, bot).Handle(new ShareDealContactCommand(deal.Id, 11), CancellationToken.None);

        Assert.Equal((11L, "+998 90 123 45 67", "Lumi Beauty"), Assert.Single(bot.Contacts));
    }

    [Fact]
    public async Task A_stranger_gets_not_found_and_nothing_is_sent()
    {
        var blogger = Blogger(11);
        var business = Business(22);
        Set(business, "Phone", "+998 90 123 45 67");
        var deal = CampaignDeal(blogger, business);
        var stranger = Blogger(33);
        var bot = new SpyBotClient();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(deal, MarketplaceRole.Blogger, 33, stranger, business, bot).Handle(new ShareDealContactCommand(deal.Id, 33), CancellationToken.None));
        Assert.Empty(bot.Contacts);
    }

    [Fact]
    public async Task Explains_a_missing_phone_and_a_bot_chat_that_cannot_be_reached()
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var deal = CampaignDeal(blogger, business);

        var noPhone = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            Handler(deal, MarketplaceRole.Blogger, 11, blogger, business, new SpyBotClient()).Handle(new ShareDealContactCommand(deal.Id, 11), CancellationToken.None));
        Set(business, "Phone", "+998 90 123 45 67");
        var blocked = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            Handler(deal, MarketplaceRole.Blogger, 11, blogger, business, new SpyBotClient { FailContacts = true }).Handle(new ShareDealContactCommand(deal.Id, 11), CancellationToken.None));

        Assert.Equal("contact_phone_missing", noPhone.Code);
        Assert.Equal("contact_share_failed", blocked.Code);
    }

    private static ShareDealContactHandler Handler(Deal deal, MarketplaceRole role, long actor, BloggerProfile blogger, BusinessProfile business, SpyBotClient bot) =>
        new(new FakeDeals(deal), new FakeUsers(User(actor, role)), new FakeBloggers(blogger), new FakeBusinesses(business), bot);
}
