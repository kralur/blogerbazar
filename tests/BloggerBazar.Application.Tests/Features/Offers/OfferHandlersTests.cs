using System.Reflection;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Offers;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Offers;

public sealed class OfferHandlersTests
{
    [Fact]
    public async Task Business_sends_an_offer_that_expires_in_48_hours_and_notifies_the_blogger()
    {
        var blogger = ApprovedBlogger(11);
        var business = Business(22, "Lumi");
        var offers = new FakeOffers();
        var bot = new SpyBotClient();
        var handler = CreateHandler(offers, blogger, business, User(22, MarketplaceRole.Business), bot);

        var result = await handler.Handle(Command(22, blogger.Id), CancellationToken.None);

        var offer = Assert.Single(offers.Items);
        Assert.Equal(OfferStates.Pending, result.State);
        Assert.Equal(OfferFormats.Reels, result.Format);
        Assert.Equal(1_500_000, result.OfferedBudget);
        Assert.Equal(blogger.Name, result.CounterpartyName);
        Assert.False(result.CanRespond);
        Assert.Equal(offer.CreatedAtUtc.AddHours(48), offer.ExpiresAtUtc);
        Assert.Equal([blogger.TelegramUserId], bot.NotifiedChats);
    }

    [Theory]
    [InlineData(MarketplaceRole.Blogger)]
    [InlineData(MarketplaceRole.BrandFace)]
    public async Task Only_the_business_role_can_send_offers(MarketplaceRole role)
    {
        var blogger = ApprovedBlogger(11);
        var business = Business(22);
        var handler = CreateHandler(new FakeOffers(), blogger, business, User(22, role));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(Command(22, blogger.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Offer_to_an_unapproved_blogger_is_not_found()
    {
        var blogger = Blogger(11);
        var business = Business(22);
        var handler = CreateHandler(new FakeOffers(), blogger, business, User(22, MarketplaceRole.Business));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Command(22, blogger.Id), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Second_active_offer_to_the_same_blogger_is_a_conflict()
    {
        var blogger = ApprovedBlogger(11);
        var business = Business(22);
        var offers = new FakeOffers();
        var handler = CreateHandler(offers, blogger, business, User(22, MarketplaceRole.Business));
        await handler.Handle(Command(22, blogger.Id), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => handler.Handle(Command(22, blogger.Id), CancellationToken.None));

        Assert.Equal("offer_already_active", exception.Code);
        Assert.DoesNotContain("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(offers.Items);
    }

    [Fact]
    public async Task Expired_pending_offer_is_closed_when_a_new_one_is_sent()
    {
        var blogger = ApprovedBlogger(11);
        var business = Business(22);
        var offers = new FakeOffers();
        var handler = CreateHandler(offers, blogger, business, User(22, MarketplaceRole.Business));
        await handler.Handle(Command(22, blogger.Id), CancellationToken.None);
        var first = Assert.Single(offers.Items);
        Set(first, nameof(CollaborationRequest.ExpiresAtUtc), (DateTime?)DateTime.UtcNow.AddMinutes(-1));

        await handler.Handle(Command(22, blogger.Id), CancellationToken.None);

        Assert.Equal(CollaborationRequestStatus.Expired, first.Status);
        Assert.Equal(2, offers.Items.Count);
    }

    [Fact]
    public async Task Daily_offer_limit_is_enforced()
    {
        var blogger = ApprovedBlogger(11);
        var business = Business(22);
        var offers = new FakeOffers { SentToday = 20 };
        var handler = CreateHandler(offers, blogger, business, User(22, MarketplaceRole.Business));

        var exception = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => handler.Handle(Command(22, blogger.Id), CancellationToken.None));

        Assert.Equal("offer_daily_limit", exception.Code);
        Assert.Contains("limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(offers.Items);
    }

    [Fact]
    public async Task Blogger_accepts_an_offer_once_and_a_deal_is_created()
    {
        var (blogger, business, offer) = PendingOffer();
        var offers = new FakeOffers(offer);
        var deals = new FakeDeals();
        var bot = new SpyBotClient();
        var handler = AcceptHandler(offers, deals, blogger, business, User(11, MarketplaceRole.Blogger), bot);

        var first = await handler.Handle(new AcceptOfferCommand(11, offer.Id), CancellationToken.None);
        offers.AttachDeal(offer, deals.Added.Single());
        var second = await handler.Handle(new AcceptOfferCommand(11, offer.Id), CancellationToken.None);

        var deal = Assert.Single(deals.Added);
        Assert.Equal(OfferStates.Accepted, first.State);
        Assert.Equal(deal.Id, first.DealId);
        Assert.Equal(deal.Id, second.DealId);
        Assert.Equal(offer.Id, deal.CollaborationRequestId);
        Assert.Equal(CollaborationRequestStatus.Accepted, offer.Status);
        Assert.Equal([business.TelegramUserId], bot.NotifiedChats);
    }

    [Fact]
    public async Task Business_cannot_accept_its_own_offer()
    {
        var (blogger, business, offer) = PendingOffer();
        var deals = new FakeDeals();
        var handler = AcceptHandler(new FakeOffers(offer), deals, blogger, business, User(22, MarketplaceRole.Business));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AcceptOfferCommand(22, offer.Id), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(deals.Added);
        Assert.Equal(CollaborationRequestStatus.Sent, offer.Status);
    }

    [Fact]
    public async Task Foreign_offer_is_not_found()
    {
        var (blogger, business, offer) = PendingOffer();
        var outsider = ApprovedBlogger(33);
        var handler = AcceptHandler(new FakeOffers(offer), new FakeDeals(), outsider, business, User(33, MarketplaceRole.Blogger));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AcceptOfferCommand(33, offer.Id), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expired_offer_cannot_be_accepted()
    {
        var (blogger, business, offer) = PendingOffer();
        Set(offer, nameof(CollaborationRequest.ExpiresAtUtc), (DateTime?)DateTime.UtcNow.AddMinutes(-1));
        var deals = new FakeDeals();
        var handler = AcceptHandler(new FakeOffers(offer), deals, blogger, business, User(11, MarketplaceRole.Blogger));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new AcceptOfferCommand(11, offer.Id), CancellationToken.None));

        Assert.Contains("expired", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CollaborationRequestStatus.Expired, offer.Status);
        Assert.Empty(deals.Added);
    }

    [Fact]
    public async Task Blogger_declines_an_offer()
    {
        var (blogger, business, offer) = PendingOffer();
        var handler = new DeclineOfferHandler(new FakeUsers(User(11, MarketplaceRole.Blogger)), new FakeBloggers(blogger), new FakeBusinesses(business), new FakeOffers(offer), new SpyUnitOfWork());

        var result = await handler.Handle(new DeclineOfferCommand(11, offer.Id), CancellationToken.None);

        Assert.Equal(OfferStates.Declined, result.State);
        Assert.Equal(CollaborationRequestStatus.Declined, offer.Status);
    }

    [Fact]
    public async Task Offers_are_listed_for_the_selected_side_with_expiry_and_response_rights()
    {
        var (blogger, business, offer) = PendingOffer();
        var (_, _, expired) = PendingOffer(blogger, business);
        Set(expired, nameof(CollaborationRequest.ExpiresAtUtc), (DateTime?)DateTime.UtcNow.AddMinutes(-1));
        var offers = new FakeOffers(offer, expired);

        var asBlogger = await new GetMyOffersHandler(new FakeUsers(User(11, MarketplaceRole.Blogger)), new FakeBloggers(blogger), new FakeBusinesses(business), offers)
            .Handle(new GetMyOffersQuery(11), CancellationToken.None);
        var asBrandFace = await new GetMyOffersHandler(new FakeUsers(User(11, MarketplaceRole.BrandFace)), new FakeBloggers(blogger), new FakeBusinesses(business), offers)
            .Handle(new GetMyOffersQuery(11), CancellationToken.None);

        Assert.Equal(2, asBlogger.Count);
        var pending = Assert.Single(asBlogger, item => item.Id == offer.Id);
        Assert.True(pending.CanRespond);
        Assert.Equal(business.Name, pending.CounterpartyName);
        Assert.Equal(OfferStates.Expired, Assert.Single(asBlogger, item => item.Id == expired.Id).State);
        Assert.Empty(asBrandFace);
    }

    [Fact]
    public void Offer_migration_adds_only_nullable_columns_and_a_pending_uniqueness_index()
    {
        var migrationBuilder = new MigrationBuilder("Npgsql");
        typeof(AddCollaborationOfferTerms).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new AddCollaborationOfferTerms(), [migrationBuilder]);

        var columns = migrationBuilder.Operations.OfType<AddColumnOperation>().ToArray();
        var index = Assert.Single(migrationBuilder.Operations.OfType<CreateIndexOperation>());
        Assert.Equal(4, columns.Length);
        Assert.All(columns, column => Assert.True(column.IsNullable));
        Assert.All(columns, column => Assert.Equal("collaboration_requests", column.Table));
        Assert.True(index.IsUnique);
        Assert.Contains("\"ExpiresAtUtc\" IS NOT NULL", index.Filter);
        Assert.Equal(5, migrationBuilder.Operations.Count);
    }

    // D48: a business sends a brand face an offer in a brand face format; the brand face accepts or declines it.
    [Fact]
    public async Task Business_sends_a_brand_face_an_offer_in_a_brand_face_format()
    {
        var brandFace = BrandFaceProfile.Create(33, "Dilnoza", "tashkent", ["beauty"]);
        var business = Business(22, "Lumi");
        var offers = new FakeOffers();
        var bot = new SpyBotClient();
        var handler = new CreateOfferHandler(new FakeUsers(User(22, MarketplaceRole.Business)), new FakeBloggers(), new FakeBusinesses(business), offers, new SpyUnitOfWork(), bot, brandFaces: new FakeBrandFaces(brandFace));

        var result = await handler.Handle(BrandFaceCommand(22, brandFace.Id), CancellationToken.None);

        var offer = Assert.Single(offers.Items);
        Assert.Equal(brandFace.Id, offer.BrandFaceId);
        Assert.Null(offer.BloggerId);
        Assert.Equal(CollaborationFormat.PhotoShoot, offer.Format);
        Assert.Equal("Dilnoza", result.CounterpartyName);
        Assert.Equal("brandFace", result.CounterpartyRole);
        Assert.Equal([33L], bot.NotifiedChats);
    }

    [Fact]
    public async Task A_second_active_offer_to_the_same_brand_face_is_a_conflict()
    {
        var brandFace = BrandFaceProfile.Create(33, "Dilnoza", "tashkent", ["beauty"]);
        var business = Business(22);
        var offers = new FakeOffers();
        var handler = new CreateOfferHandler(new FakeUsers(User(22, MarketplaceRole.Business)), new FakeBloggers(), new FakeBusinesses(business), offers, new SpyUnitOfWork(), brandFaces: new FakeBrandFaces(brandFace));
        await handler.Handle(BrandFaceCommand(22, brandFace.Id), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => handler.Handle(BrandFaceCommand(22, brandFace.Id), CancellationToken.None));

        Assert.Equal("offer_already_active", exception.Code);
    }

    [Theory]
    [InlineData(true, OfferFormats.Reels)]
    [InlineData(false, "photoShoot")]
    [InlineData(true, "dancing")]
    public void Offer_format_must_fit_the_recipient(bool toBrandFace, string format)
    {
        var command = toBrandFace ? BrandFaceCommand(22, Guid.NewGuid()) with { Format = format } : Command(22, Guid.NewGuid()) with { Format = format };

        var result = new CreateOfferValidator().Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateOfferCommand.Format));
    }

    [Fact]
    public void An_offer_has_exactly_one_recipient()
    {
        var validator = new CreateOfferValidator();

        Assert.False(validator.Validate(Command(22, Guid.NewGuid()) with { BrandFaceId = Guid.NewGuid() }).IsValid);
        Assert.False(validator.Validate(new CreateOfferCommand(22, null, "photoShoot", null, null, "Hi")).IsValid);
        Assert.True(validator.Validate(BrandFaceCommand(22, Guid.NewGuid())).IsValid);
    }

    [Fact]
    public async Task Offer_to_a_missing_brand_face_is_not_found()
    {
        var handler = new CreateOfferHandler(new FakeUsers(User(22, MarketplaceRole.Business)), new FakeBloggers(), new FakeBusinesses(Business(22)), new FakeOffers(), new SpyUnitOfWork(), brandFaces: new FakeBrandFaces());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(BrandFaceCommand(22, Guid.NewGuid()), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Brand_face_accepts_its_offer_and_gets_a_deal()
    {
        var brandFace = BrandFaceProfile.Create(33, "Dilnoza", "tashkent", ["beauty"]);
        var business = Business(22, "Lumi");
        var offer = CollaborationRequest.CreateOfferForBrandFace(brandFace.Id, business.Id, "Shoot", CollaborationFormat.PhotoShoot, 500_000, null);
        Set(offer, nameof(CollaborationRequest.Business), business);
        Set(offer, nameof(CollaborationRequest.BrandFace), brandFace);
        var deals = new FakeDeals();
        var handler = new AcceptOfferHandler(new FakeUsers(User(33, MarketplaceRole.BrandFace)), new FakeBloggers(), new FakeBusinesses(business), new FakeOffers(offer), deals, new SpyUnitOfWork(), brandFaces: new FakeBrandFaces(brandFace));

        var result = await handler.Handle(new AcceptOfferCommand(33, offer.Id), CancellationToken.None);

        var deal = Assert.Single(deals.Added);
        Assert.Equal(OfferStates.Accepted, result.State);
        Assert.Equal(brandFace.Id, deal.BrandFaceId);
        Assert.Null(deal.BloggerId);
    }

    [Fact]
    public async Task A_blogger_profile_of_the_same_account_cannot_answer_a_brand_face_offer()
    {
        var brandFace = BrandFaceProfile.Create(33, "Dilnoza", "tashkent", ["beauty"]);
        var blogger = ApprovedBlogger(33);
        var business = Business(22);
        var offer = CollaborationRequest.CreateOfferForBrandFace(brandFace.Id, business.Id, "Shoot", CollaborationFormat.PhotoShoot, null, null);
        var handler = new DeclineOfferHandler(new FakeUsers(User(33, MarketplaceRole.Blogger)), new FakeBloggers(blogger), new FakeBusinesses(business), new FakeOffers(offer), new SpyUnitOfWork(), brandFaces: new FakeBrandFaces(brandFace));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new DeclineOfferCommand(33, offer.Id), CancellationToken.None));
        Assert.True(offer.IsPending);
    }

    private static CreateOfferCommand BrandFaceCommand(long telegramUserId, Guid brandFaceId) =>
        new(telegramUserId, null, "photoShoot", 500_000, null, "Photo shoot for our spring line", brandFaceId);

    private static CreateOfferCommand Command(long telegramUserId, Guid bloggerId) =>
        new(telegramUserId, bloggerId, OfferFormats.Reels, 1_500_000, null, "One reel about our launch");

    private static CreateOfferHandler CreateHandler(FakeOffers offers, BloggerProfile blogger, BusinessProfile business, PlatformUser actor, SpyBotClient? bot = null) =>
        new(new FakeUsers(actor), new FakeBloggers(blogger), new FakeBusinesses(business), offers, new SpyUnitOfWork(), bot);

    private static AcceptOfferHandler AcceptHandler(FakeOffers offers, FakeDeals deals, BloggerProfile blogger, BusinessProfile business, PlatformUser actor, SpyBotClient? bot = null) =>
        new(new FakeUsers(actor), new FakeBloggers(blogger), new FakeBusinesses(business), offers, deals, new SpyUnitOfWork(), bot);

    private static BloggerProfile ApprovedBlogger(long telegramUserId)
    {
        var blogger = Blogger(telegramUserId);
        blogger.Approve();
        return blogger;
    }

    private static (BloggerProfile Blogger, BusinessProfile Business, CollaborationRequest Offer) PendingOffer(BloggerProfile? blogger = null, BusinessProfile? business = null)
    {
        blogger ??= ApprovedBlogger(11);
        business ??= Business(22, "Lumi");
        var offer = CollaborationRequest.CreateOffer(blogger.Id, business.Id, "One reel", CollaborationFormat.Reels, 100, null);
        Set(offer, nameof(CollaborationRequest.Blogger), blogger);
        Set(offer, nameof(CollaborationRequest.Business), business);
        return (blogger, business, offer);
    }

    private sealed class FakeOffers(params CollaborationRequest[] seed) : ICollaborationRequestRepository
    {
        public List<CollaborationRequest> Items { get; } = [.. seed];
        public int SentToday { get; init; }

        public Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        public Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(CollaborationRequest request, CancellationToken cancellationToken) { Items.Add(request); return Task.CompletedTask; }

        public Task<CollaborationRequest?> GetOfferForParticipantAsync(Guid offerId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == offerId && Matches(item, role, profileId)));

        public Task<IReadOnlyList<CollaborationRequest>> ListOffersForParticipantAsync(MarketplaceRole role, Guid profileId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CollaborationRequest>>(Items.Where(item => Matches(item, role, profileId)).Take(take).ToArray());

        public Task<CollaborationRequest?> GetPendingOfferAsync(Guid businessId, MarketplaceRole creatorRole, Guid creatorId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.BusinessId == businessId && item.CreatorRole == creatorRole && item.CreatorId == creatorId && item.IsOffer && item.IsPending));

        public Task<int> CountOffersSinceAsync(Guid businessId, DateTime sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(SentToday + Items.Count(item => item.BusinessId == businessId && item.CreatedAtUtc >= sinceUtc));

        public void AttachDeal(CollaborationRequest offer, Deal deal) => Set(offer, nameof(CollaborationRequest.Deal), deal);

        private static bool Matches(CollaborationRequest item, MarketplaceRole role, Guid profileId) => role switch
        {
            MarketplaceRole.Blogger => item.BloggerId == profileId,
            MarketplaceRole.BrandFace => item.BrandFaceId == profileId,
            MarketplaceRole.Business => item.BusinessId == profileId,
            _ => false
        };
    }
}
