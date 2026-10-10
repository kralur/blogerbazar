using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Deals;

public sealed class DealResultsTests
{
    private const long BloggerTelegramId = 11;
    private const long BusinessTelegramId = 22;

    [Fact]
    public async Task Creator_adds_a_publication_and_the_business_is_notified()
    {
        var world = World();
        var bot = new SpyBotClient();

        var result = await world.Add(BloggerTelegramId, " https://instagram.com/p/abc ", 1200, bot);

        Assert.Equal("https://instagram.com/p/abc", result.Url);
        Assert.Equal(1200, result.Views);
        Assert.False(result.Confirmed);
        Assert.Single(world.Publications.Items);
        Assert.Equal([BusinessTelegramId], bot.NotifiedChats);
        Assert.Equal($"/deal/{world.Deal.Id}", bot.Routes.Single());
    }

    [Fact]
    public async Task Business_cannot_add_a_publication()
    {
        var world = World();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => world.Add(BusinessTelegramId, "https://instagram.com/p/abc", null));
        Assert.Empty(world.Publications.Items);
    }

    [Fact]
    public async Task A_foreign_deal_reads_as_not_found()
    {
        var world = World();
        var stranger = Blogger(33);
        var handler = new AddDealPublicationHandler(
            new FakeUsers(User(33, MarketplaceRole.Blogger)), new FakeBloggers(world.Blogger, stranger), new FakeBusinesses(world.Business),
            new FakeDeals(world.Deal), world.Publications, new SpyUnitOfWork());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AddDealPublicationCommand(world.Deal.Id, 33, "https://instagram.com/p/abc", null), CancellationToken.None));
        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_same_link_counts_once()
    {
        var world = World();
        await world.Add(BloggerTelegramId, "https://instagram.com/p/abc", 10);

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => world.Add(BloggerTelegramId, "https://instagram.com/p/abc", 20));
        Assert.Equal(DealResultCodes.PublicationDuplicate, error.Code);
    }

    [Fact]
    public async Task A_deal_holds_at_most_five_publications()
    {
        var world = World();
        for (var index = 0; index < 5; index++) await world.Add(BloggerTelegramId, $"https://instagram.com/p/{index}", null);

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => world.Add(BloggerTelegramId, "https://instagram.com/p/6", null));
        Assert.Equal(DealResultCodes.PublicationLimit, error.Code);
    }

    [Fact]
    public async Task A_cancelled_deal_takes_no_results()
    {
        var world = World();
        Set(world.Deal, nameof(Deal.Status), DealStatus.Cancelled);

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.Add(BloggerTelegramId, "https://instagram.com/p/abc", null));
    }

    [Fact]
    public async Task A_completed_deal_still_takes_results()
    {
        var world = World();
        world.Deal.Complete();

        var result = await world.Add(BloggerTelegramId, "https://instagram.com/p/abc", 5);

        Assert.Equal(5, result.Views);
    }

    [Fact]
    public async Task Business_confirms_and_new_views_need_a_new_confirmation()
    {
        var world = World();
        var added = await world.Add(BloggerTelegramId, "https://instagram.com/p/abc", 100);

        var confirmed = await world.Confirm(BusinessTelegramId, added.Id);
        Assert.True(confirmed.Confirmed);

        var updated = await new UpdateDealPublicationViewsHandler(world.Users, world.Bloggers, world.Businesses, world.Deals, world.Publications, new SpyUnitOfWork())
            .Handle(new UpdateDealPublicationViewsCommand(world.Deal.Id, added.Id, BloggerTelegramId, 150), CancellationToken.None);

        Assert.Equal(150, updated.Views);
        Assert.False(updated.Confirmed);
    }

    [Fact]
    public async Task Creator_cannot_confirm_own_numbers()
    {
        var world = World();
        var added = await world.Add(BloggerTelegramId, "https://instagram.com/p/abc", 100);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => world.Confirm(BloggerTelegramId, added.Id));
    }

    [Fact]
    public async Task A_confirmed_publication_cannot_be_deleted_but_an_unconfirmed_one_can()
    {
        var world = World();
        var first = await world.Add(BloggerTelegramId, "https://instagram.com/p/1", 100);
        var second = await world.Add(BloggerTelegramId, "https://instagram.com/p/2", 100);
        await world.Confirm(BusinessTelegramId, first.Id);
        var handler = new DeleteDealPublicationHandler(world.Users, world.Bloggers, world.Businesses, world.Deals, world.Publications, new SpyUnitOfWork());

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() =>
            handler.Handle(new DeleteDealPublicationCommand(world.Deal.Id, first.Id, BloggerTelegramId), CancellationToken.None));
        Assert.Equal(DealResultCodes.PublicationConfirmed, error.Code);

        await handler.Handle(new DeleteDealPublicationCommand(world.Deal.Id, second.Id, BloggerTelegramId), CancellationToken.None);
        Assert.Equal([first.Id], world.Publications.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Another_deals_publication_reads_as_not_found()
    {
        var world = World();
        var other = World();
        var foreign = await other.Add(BloggerTelegramId, "https://instagram.com/p/abc", 1);
        world.Publications.Items.AddRange(other.Publications.Items);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => world.Confirm(BusinessTelegramId, foreign.Id));
        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Business_sets_the_price_of_an_application_deal()
    {
        var world = World();
        var unitOfWork = new SpyUnitOfWork();

        var result = await new SetDealPriceHandler(world.Users, world.Bloggers, world.Businesses, world.Deals, unitOfWork)
            .Handle(new SetDealPriceCommand(world.Deal.Id, BusinessTelegramId, 2_500_000), CancellationToken.None);

        Assert.Equal(2_500_000, result.AgreedPrice);
        Assert.Equal(2_500_000, world.Deal.AgreedPrice);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Creator_cannot_set_the_price()
    {
        var world = World();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new SetDealPriceHandler(world.Users, world.Bloggers, world.Businesses, world.Deals, new SpyUnitOfWork())
            .Handle(new SetDealPriceCommand(world.Deal.Id, BloggerTelegramId, 1), CancellationToken.None));
        Assert.Null(world.Deal.AgreedPrice);
    }

    [Fact]
    public async Task An_offer_budget_fixes_the_price()
    {
        var blogger = Blogger(BloggerTelegramId);
        var business = Business(BusinessTelegramId);
        var offer = CollaborationRequest.CreateOffer(blogger.Id, business.Id, "Message", CollaborationFormat.Post, 900_000, null);
        var deal = Attach(Deal.CreateFromCollaborationRequest(offer.Id, MarketplaceRole.Blogger, blogger.Id, business.Id), blogger, business);
        Set(deal, nameof(Deal.CollaborationRequest), offer);
        var users = new FakeUsers(User(BusinessTelegramId, MarketplaceRole.Business));

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => new SetDealPriceHandler(users, new FakeBloggers(blogger), new FakeBusinesses(business), new FakeDeals(deal), new SpyUnitOfWork())
            .Handle(new SetDealPriceCommand(deal.Id, BusinessTelegramId, 1), CancellationToken.None));
        Assert.Equal(DealResultCodes.PriceFixedByOffer, error.Code);
    }

    [Fact]
    public async Task Validator_accepts_only_https_links()
    {
        var validator = new AddDealPublicationValidator();

        Assert.True((await validator.ValidateAsync(new AddDealPublicationCommand(Guid.NewGuid(), 1, "https://t.me/channel/5", 10))).IsValid);
        Assert.False((await validator.ValidateAsync(new AddDealPublicationCommand(Guid.NewGuid(), 1, "http://t.me/channel/5", 10))).IsValid);
        Assert.False((await validator.ValidateAsync(new AddDealPublicationCommand(Guid.NewGuid(), 1, "not a link", 10))).IsValid);
        Assert.False((await validator.ValidateAsync(new AddDealPublicationCommand(Guid.NewGuid(), 1, "https://t.me/channel/5", -1))).IsValid);
    }

    [Fact]
    public async Task Details_show_the_offer_budget_as_the_price_and_who_may_act()
    {
        var bloggerProfile = Blogger(BloggerTelegramId);
        var businessProfile = Business(BusinessTelegramId);
        var row = new DealReadRow(
            Guid.NewGuid(), null, Guid.NewGuid(), DealStatus.Active, DateTime.UtcNow, null, "Blogger", null, "Business", null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, false, false,
            CollaborationFormat.Post, 700_000, null, "Message", bloggerProfile.Id, businessProfile.Id, AgreedPrice: 1);
        var users = new FakeUsers(User(BloggerTelegramId, MarketplaceRole.Blogger), User(BusinessTelegramId, MarketplaceRole.Business));
        var handler = new GetMyDealHandler(users, new FakeBloggers(bloggerProfile), new FakeBusinesses(businessProfile), new FakeDealReadModel(row), publications: new FakeDealPublications());

        var business = await handler.Handle(new GetMyDealQuery(row.Id, BusinessTelegramId), CancellationToken.None);
        var blogger = await handler.Handle(new GetMyDealQuery(row.Id, BloggerTelegramId), CancellationToken.None);

        Assert.Equal(700_000, business.AgreedPrice);
        Assert.False(business.CanSetPrice);
        Assert.True(business.CanConfirmPublications);
        Assert.False(business.CanAddPublication);
        Assert.True(blogger.CanAddPublication);
        Assert.False(blogger.CanConfirmPublications);
        Assert.Empty(blogger.Publications!);
    }

    private static TestWorld World()
    {
        var blogger = Blogger(BloggerTelegramId);
        var business = Business(BusinessTelegramId);
        return new TestWorld(blogger, business, CampaignDeal(blogger, business));
    }

    private sealed class TestWorld(BloggerProfile blogger, BusinessProfile business, Deal deal)
    {
        public BloggerProfile Blogger { get; } = blogger;
        public BusinessProfile Business { get; } = business;
        public Deal Deal { get; } = deal;
        public FakeUsers Users { get; } = new(User(BloggerTelegramId, MarketplaceRole.Blogger), User(BusinessTelegramId, MarketplaceRole.Business));
        public FakeBloggers Bloggers { get; } = new(blogger);
        public FakeBusinesses Businesses { get; } = new(business);
        public FakeDeals Deals { get; } = new(deal);
        public FakeDealPublications Publications { get; } = new();

        public Task<DealPublicationDto> Add(long telegramUserId, string url, int? views, SpyBotClient? bot = null) =>
            new AddDealPublicationHandler(Users, Bloggers, Businesses, Deals, Publications, new SpyUnitOfWork(), botClient: bot)
                .Handle(new AddDealPublicationCommand(Deal.Id, telegramUserId, url, views), CancellationToken.None);

        public Task<DealPublicationDto> Confirm(long telegramUserId, Guid publicationId) =>
            new ConfirmDealPublicationHandler(Users, Bloggers, Businesses, Deals, Publications, new SpyUnitOfWork())
                .Handle(new ConfirmDealPublicationCommand(Deal.Id, publicationId, telegramUserId), CancellationToken.None);
    }
}

internal sealed class FakeDealPublications : IDealPublicationRepository
{
    public List<DealPublication> Items { get; } = [];

    public Task<IReadOnlyList<DealPublication>> ListForDealAsync(Guid dealId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DealPublication>>(Items.Where(item => item.DealId == dealId).ToArray());

    public Task<DealPublication?> GetForDealAsync(Guid dealId, Guid publicationId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(item => item.Id == publicationId && item.DealId == dealId));

    public Task AddAsync(DealPublication publication, CancellationToken cancellationToken) { Items.Add(publication); return Task.CompletedTask; }

    public void Remove(DealPublication publication) => Items.Remove(publication);
}
