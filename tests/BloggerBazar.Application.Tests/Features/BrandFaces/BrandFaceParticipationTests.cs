using System.Reflection;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.BrandFaces;

// D46: a brand face applies to campaigns, gets deals and reviews exactly like a blogger.
public sealed class BrandFaceParticipationTests
{
    [Fact]
    public async Task Brand_face_applies_to_a_campaign_with_its_own_profile()
    {
        var business = ApprovedBusiness(99);
        var campaign = PublishedCampaign(business);
        var brandFace = BrandFace(12, "Dilnoza");
        var applications = new Applications();
        var bot = new SpyBotClient();
        var handler = new ApplyToCampaignHandler(new Campaigns(campaign), new FakeUsers(User(12, MarketplaceRole.BrandFace), User(99, MarketplaceRole.Business)),
            new FakeBloggers(), new FakeBusinesses(), applications, new SpyUnitOfWork(), botClient: bot, brandFaces: new FakeBrandFaces(brandFace));

        var result = await handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, "Hello"), CancellationToken.None);

        var application = Assert.Single(applications.Values);
        Assert.Null(application.BloggerId);
        Assert.Equal(brandFace.Id, application.BrandFaceId);
        Assert.Equal(brandFace.Id, result.BrandFaceId);
        Assert.Null(result.BloggerId);
        Assert.Contains("Dilnoza", Assert.Single(bot.Texts));
    }

    [Fact]
    public async Task Brand_face_role_without_a_brand_face_profile_cannot_apply_even_with_a_blogger_profile()
    {
        var business = ApprovedBusiness(99);
        var campaign = PublishedCampaign(business);
        var blogger = Blogger(12);
        blogger.Approve();
        var applications = new Applications();
        var handler = new ApplyToCampaignHandler(new Campaigns(campaign), new FakeUsers(User(12, MarketplaceRole.BrandFace), User(99, MarketplaceRole.Business)),
            new FakeBloggers(blogger), new FakeBusinesses(), applications, new SpyUnitOfWork(), brandFaces: new FakeBrandFaces());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new ApplyToCampaignCommand(campaign.Id, 12, null), CancellationToken.None));
        Assert.Empty(applications.Values);
    }

    [Fact]
    public async Task Brand_face_lists_only_its_own_applications()
    {
        var brandFace = BrandFace(12);
        var readModel = new CapturingApplicationReadModel();
        var handler = new GetMyCampaignApplicationsPageHandler(new FakeUsers(User(12, MarketplaceRole.BrandFace)), new FakeBloggers(), readModel, new FakeBrandFaces(brandFace));

        await handler.Handle(new GetMyCampaignApplicationsPageQuery(12, null, 1, 20), CancellationToken.None);

        Assert.Equal((MarketplaceRole.BrandFace, brandFace.Id), readModel.Creator);
    }

    [Fact]
    public async Task Accepting_a_brand_face_application_opens_a_deal_with_the_brand_face()
    {
        var business = ApprovedBusiness(99);
        var campaign = PublishedCampaign(business);
        var brandFace = BrandFace(12);
        var application = CampaignApplication.CreateForBrandFace(campaign.Id, brandFace.Id, null);
        Set(application, nameof(CampaignApplication.Campaign), campaign);
        var deals = new FakeDeals();
        var bot = new SpyBotClient();
        var handler = new DecideCampaignApplicationHandler(new FakeUsers(User(99, MarketplaceRole.Business)), new FakeBusinesses(business), new Campaigns(campaign),
            new Applications(application), deals, new SpyUnitOfWork(), new FakeBloggers(), bot, brandFaces: new FakeBrandFaces(brandFace));

        var result = await handler.Handle(new DecideCampaignApplicationCommand(99, campaign.Id, application.Id, CampaignApplicationStatus.Accepted), CancellationToken.None);

        var deal = Assert.Single(deals.Added);
        Assert.Equal(result.DealId, deal.Id);
        Assert.Null(deal.BloggerId);
        Assert.Equal(brandFace.Id, deal.BrandFaceId);
        Assert.Equal([12L], bot.NotifiedChats);
    }

    [Fact]
    public async Task Brand_face_sees_its_deal_with_the_business_as_counterparty()
    {
        var brandFace = BrandFace(12);
        var business = Business(99, "Lumi");
        var row = Row(brandFace, business);
        var readModel = new FakeDealReadModel(row);
        var handler = new GetMyDealHandler(new FakeUsers(User(12, MarketplaceRole.BrandFace)), new FakeBloggers(), new FakeBusinesses(), readModel, new FakeBrandFaces(brandFace));

        var details = await handler.Handle(new GetMyDealQuery(row.Id, 12), CancellationToken.None);

        Assert.Equal((MarketplaceRole.BrandFace, brandFace.Id), Assert.Single(readModel.Queries));
        Assert.Equal("Lumi", details.CounterpartyName);
        Assert.Equal(business.Id, details.CounterpartyProfileId);
        Assert.Equal("business", details.CounterpartyRole);
        Assert.True(details.CanComplete);
    }

    [Fact]
    public async Task Business_sees_the_brand_face_as_counterparty()
    {
        var brandFace = BrandFace(12, "Dilnoza");
        var business = Business(99);
        var row = Row(brandFace, business);
        var handler = new GetMyDealHandler(new FakeUsers(User(99, MarketplaceRole.Business)), new FakeBloggers(), new FakeBusinesses(business), new FakeDealReadModel(row));

        var details = await handler.Handle(new GetMyDealQuery(row.Id, 99), CancellationToken.None);

        Assert.Equal("Dilnoza", details.CounterpartyName);
        Assert.Equal(brandFace.Id, details.CounterpartyProfileId);
        Assert.Equal("brandFace", details.CounterpartyRole);
    }

    [Fact]
    public async Task Without_a_brand_face_profile_the_brand_face_role_has_no_deals()
    {
        var handler = new GetMyDealsHandler(new FakeUsers(User(12, MarketplaceRole.BrandFace)), new FakeBloggers(Blogger(12)), new FakeBusinesses(), new FakeDealReadModel(), new FakeBrandFaces());

        Assert.Empty(await handler.Handle(new GetMyDealsQuery(12), CancellationToken.None));
    }

    [Fact]
    public async Task Business_review_of_a_brand_face_deal_targets_the_brand_face()
    {
        var brandFace = BrandFace(12, "Dilnoza");
        var business = Business(99, "Lumi");
        var deal = CompletedDeal(brandFace, business);
        var reviews = new Reviews();
        var bot = new SpyBotClient();
        var handler = new CreateReviewHandler(new FakeDeals(deal), new FakeUsers(User(99, MarketplaceRole.Business)), new FakeBloggers(), new FakeBusinesses(business), reviews, new SpyUnitOfWork(), bot, brandFaces: new FakeBrandFaces(brandFace));

        var result = await handler.Handle(new CreateReviewCommand(deal.Id, 99, 5, null), CancellationToken.None);

        var review = Assert.Single(reviews.Values);
        Assert.Equal(ReviewTargetType.BrandFace, review.TargetType);
        Assert.Equal(brandFace.Id, review.BrandFaceId);
        Assert.Null(review.BloggerId);
        Assert.Equal((int)ReviewTargetType.BrandFace, result.TargetType);
        Assert.Equal([12L], bot.NotifiedChats);
    }

    [Fact]
    public async Task Brand_face_reviews_the_business_of_its_deal()
    {
        var brandFace = BrandFace(12, "Dilnoza");
        var business = Business(99, "Lumi");
        var deal = CompletedDeal(brandFace, business);
        var reviews = new Reviews();
        var handler = new CreateReviewHandler(new FakeDeals(deal), new FakeUsers(User(12, MarketplaceRole.BrandFace)), new FakeBloggers(), new FakeBusinesses(business), reviews, new SpyUnitOfWork(), brandFaces: new FakeBrandFaces(brandFace));

        var result = await handler.Handle(new CreateReviewCommand(deal.Id, 12, 4, null), CancellationToken.None);

        var review = Assert.Single(reviews.Values);
        Assert.Equal(ReviewTargetType.Business, review.TargetType);
        Assert.Equal(business.Id, review.BusinessId);
        Assert.Equal("Dilnoza", result.ReviewerName);
    }

    [Fact]
    public void Deal_creator_is_a_blogger_or_a_brand_face_only()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Deal.CreateFromCollaborationRequest(Guid.NewGuid(), MarketplaceRole.Business, Guid.NewGuid(), Guid.NewGuid()));
        var deal = Deal.CreateFromCollaborationRequest(Guid.NewGuid(), MarketplaceRole.BrandFace, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(MarketplaceRole.BrandFace, deal.CreatorRole);
        Assert.Null(deal.BloggerId);
    }

    [Theory]
    [InlineData(MarketplaceRole.Business, false, true, false)]
    [InlineData(MarketplaceRole.BrandFace, false, false, true)]
    [InlineData(MarketplaceRole.Blogger, true, false, false)]
    public async Task Legacy_application_list_shows_only_the_selected_role_side(MarketplaceRole role, bool blogger, bool business, bool brandFace)
    {
        var bloggerProfile = Blogger(12);
        var businessProfile = Business(12, "Abba");
        var brandFaceProfile = BrandFace(12);
        var catalog = new CapturingCatalog();
        var handler = new GetMyCampaignApplicationsHandler(new FakeBloggers(bloggerProfile), new FakeBusinesses(businessProfile), catalog,
            new FakeBrandFaces(brandFaceProfile), new FakeUsers(User(12, role)));

        await handler.Handle(new GetMyCampaignApplicationsQuery(12), CancellationToken.None);

        Assert.Equal(blogger ? bloggerProfile.Id : null, catalog.BloggerId);
        Assert.Equal(business ? businessProfile.Id : null, catalog.BusinessId);
        Assert.Equal(brandFace ? brandFaceProfile.Id : null, catalog.BrandFaceId);
    }

    [Fact]
    public async Task Application_list_is_empty_without_a_selected_role()
    {
        var catalog = new CapturingCatalog();
        var handler = new GetMyCampaignApplicationsHandler(new FakeBloggers(Blogger(12)), new FakeBusinesses(Business(12, "Abba")), catalog,
            new FakeBrandFaces(BrandFace(12)), new FakeUsers(User(12, null)));

        var result = await handler.Handle(new GetMyCampaignApplicationsQuery(12), CancellationToken.None);

        Assert.Empty(result);
        Assert.Null(catalog.BloggerId);
        Assert.Null(catalog.BusinessId);
    }

    private static BrandFaceProfile BrandFace(long telegramUserId, string name = "Brand face") => BrandFaceProfile.Create(telegramUserId, name, "tashkent", ["beauty"]);

    private static BusinessProfile ApprovedBusiness(long telegramUserId)
    {
        var business = Business(telegramUserId);
        business.Approve();
        return business;
    }

    private static Campaign PublishedCampaign(BusinessProfile business)
    {
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, null, null);
        campaign.Publish();
        Set(campaign, nameof(Campaign.Business), business);
        return campaign;
    }

    private static Deal CompletedDeal(BrandFaceProfile brandFace, BusinessProfile business)
    {
        var campaign = Campaign.Create(business.Id, "Campaign", "Description", ["beauty"], null, null, null, null, null);
        var deal = Deal.Create(Guid.NewGuid(), MarketplaceRole.BrandFace, brandFace.Id, business.Id, CampaignTermsSnapshot.FromCampaign(campaign));
        Set(deal, nameof(Deal.BrandFace), brandFace);
        Set(deal, nameof(Deal.Business), business);
        deal.Complete();
        return deal;
    }

    private static DealReadRow Row(BrandFaceProfile brandFace, BusinessProfile business) => new(
        Guid.NewGuid(), Guid.NewGuid(), null, DealStatus.Active, DateTime.UtcNow, null,
        brandFace.Name, null, business.Name, null,
        null, null, null, null, null, null, null, null, null,
        "Campaign", "Description", null, ["beauty"], [], null, null, null,
        false, false,
        BusinessId: business.Id, BrandFaceId: brandFace.Id);

    private sealed class Campaigns(Campaign campaign) : ICampaignRepository
    {
        public Task AddAsync(Campaign value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Campaign?>(id == campaign.Id ? campaign : null);
        public Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken) => Task.FromResult<Campaign?>(id == campaign.Id && campaign.BusinessId == businessId ? campaign : null);
        public Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }

    private sealed class Applications(params CampaignApplication[] initial) : ICampaignApplicationRepository
    {
        public List<CampaignApplication> Values { get; } = [.. initial];
        public Task AddAsync(CampaignApplication application, CancellationToken cancellationToken) { Values.Add(application); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(Guid campaignId, MarketplaceRole creatorRole, Guid creatorId, CancellationToken cancellationToken) =>
            Task.FromResult(Values.Any(application => application.CampaignId == campaignId && application.CreatorRole == creatorRole && application.CreatorId == creatorId));
        public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Values.SingleOrDefault(application => application.Id == id));
    }

    private sealed class CapturingApplicationReadModel : ICampaignApplicationReadModel
    {
        public (MarketplaceRole, Guid)? Creator { get; private set; }
        public Task<MyCampaignApplicationsResult> SearchForCreatorAsync(MarketplaceRole creatorRole, Guid creatorId, CampaignApplicationSearch search, CancellationToken cancellationToken)
        {
            Creator = (creatorRole, creatorId);
            return Task.FromResult(new MyCampaignApplicationsResult([], 0, search.Page, search.PageSize, false));
        }
        public Task<MyCampaignApplicationDetailsDto?> GetForCreatorAsync(MarketplaceRole creatorRole, Guid creatorId, Guid applicationId, CancellationToken cancellationToken) =>
            Task.FromResult<MyCampaignApplicationDetailsDto?>(null);
        public Task<CampaignApplicationInboxResult> SearchForBusinessAsync(Guid businessId, Guid campaignId, CampaignApplicationSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(new CampaignApplicationInboxResult([], 0, search.Page, search.PageSize, false));
    }

    private sealed class CapturingCatalog : IMarketplaceCatalogReadModel
    {
        public Guid? BloggerId { get; private set; }
        public Guid? BusinessId { get; private set; }
        public Guid? BrandFaceId { get; private set; }
        public Task<IReadOnlyList<MyCampaignApplicationDto>> GetCampaignApplicationsAsync(Guid? bloggerId, Guid? businessId, CancellationToken cancellationToken, Guid? brandFaceId = null)
        {
            (BloggerId, BusinessId, BrandFaceId) = (bloggerId, businessId, brandFaceId);
            return Task.FromResult<IReadOnlyList<MyCampaignApplicationDto>>([]);
        }
        public Task<Application.Features.Bloggers.SearchBloggersResult> SearchBloggersAsync(BloggerCatalogSearch search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Application.Features.Bloggers.BloggerProfileDto?> GetBloggerAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Application.Features.Bloggers.MyBloggerProfileDto?> GetMyBloggerAsync(long telegramUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CampaignDto>> SearchCampaignsAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CampaignDto?> GetCampaignAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Application.Features.CollaborationRequests.CollaborationRequestDto>> GetCollaborationRequestsAsync(Guid? bloggerId, Guid? businessId, int take, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Reviews : IReviewRepository
    {
        public List<Review> Values { get; } = [];
        public Task AddAsync(Review review, CancellationToken cancellationToken) { Values.Add(review); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<int> PublishRevealedAsync(Guid? dealId, DateTime nowUtc, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
