using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.Marketplace;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Marketplace;

public sealed class RecommendationsTests
{
    [Fact]
    public async Task Blogger_gets_campaigns_for_its_categories_and_city()
    {
        var blogger = Blogger(11);
        var readModel = new SpyReadModel();

        var result = await Handler(readModel, User(11, MarketplaceRole.Blogger), blogger: blogger).Handle(new GetRecommendationsQuery(11), CancellationToken.None);

        Assert.Equal(RecommendationAudiences.Creator, result.Audience);
        Assert.Equal((MarketplaceRole.Blogger, blogger.Id, "tashkent"), readModel.CreatorQuery);
        Assert.Equal(["beauty"], readModel.Categories);
    }

    [Fact]
    public async Task Brand_face_gets_campaigns_as_a_brand_face()
    {
        var brandFace = BrandFaceProfile.Create(11, "Face", "samarkand", ["fashion"]);
        var readModel = new SpyReadModel();

        var result = await Handler(readModel, User(11, MarketplaceRole.BrandFace), brandFace: brandFace).Handle(new GetRecommendationsQuery(11), CancellationToken.None);

        Assert.Equal(RecommendationAudiences.Creator, result.Audience);
        Assert.Equal((MarketplaceRole.BrandFace, brandFace.Id, "samarkand"), readModel.CreatorQuery);
    }

    [Fact]
    public async Task Business_gets_creators_for_its_own_business()
    {
        var business = Business(22);
        var readModel = new SpyReadModel();

        var result = await Handler(readModel, User(22, MarketplaceRole.Business), business: business).Handle(new GetRecommendationsQuery(22), CancellationToken.None);

        Assert.Equal(RecommendationAudiences.Business, result.Audience);
        Assert.Equal(business.Id, readModel.BusinessQuery);
    }

    [Fact]
    public async Task Selected_role_without_its_profile_gets_nothing_even_with_another_profile()
    {
        var readModel = new SpyReadModel();

        var result = await Handler(readModel, User(11, MarketplaceRole.Business), blogger: Blogger(11)).Handle(new GetRecommendationsQuery(11), CancellationToken.None);

        Assert.Equal(RecommendationAudiences.None, result.Audience);
        Assert.Null(readModel.CreatorQuery);
        Assert.Null(readModel.BusinessQuery);
    }

    [Fact]
    public async Task Blocked_account_gets_nothing()
    {
        var user = User(11, MarketplaceRole.Blogger);
        user.SetBlocked(true);
        var readModel = new SpyReadModel();

        var result = await Handler(readModel, user, blogger: Blogger(11)).Handle(new GetRecommendationsQuery(11), CancellationToken.None);

        Assert.Equal(RecommendationAudiences.None, result.Audience);
        Assert.Null(readModel.CreatorQuery);
    }

    private static GetRecommendationsHandler Handler(SpyReadModel readModel, PlatformUser user, BloggerProfile? blogger = null, BrandFaceProfile? brandFace = null, BusinessProfile? business = null) =>
        new(new FakeUsers(user),
            blogger is null ? new FakeBloggers() : new FakeBloggers(blogger),
            brandFace is null ? new FakeBrandFaces() : new FakeBrandFaces(brandFace),
            business is null ? new FakeBusinesses() : new FakeBusinesses(business),
            readModel);

    private sealed class SpyReadModel : IMarketplaceRecommendationReadModel
    {
        public (MarketplaceRole, Guid, string?)? CreatorQuery { get; private set; }
        public IReadOnlyCollection<string>? Categories { get; private set; }
        public Guid? BusinessQuery { get; private set; }

        public Task<IReadOnlyList<CampaignDto>> CampaignsForCreatorAsync(MarketplaceRole creatorRole, Guid creatorId, IReadOnlyCollection<string> categories, string? city, int take, CancellationToken cancellationToken)
        {
            CreatorQuery = (creatorRole, creatorId, city);
            Categories = categories;
            return Task.FromResult<IReadOnlyList<CampaignDto>>([]);
        }

        public Task<(IReadOnlyList<BloggerProfileDto> Bloggers, IReadOnlyList<BrandFacePublicDto> BrandFaces)> CreatorsForBusinessAsync(Guid businessId, int take, CancellationToken cancellationToken)
        {
            BusinessQuery = businessId;
            return Task.FromResult<(IReadOnlyList<BloggerProfileDto>, IReadOnlyList<BrandFacePublicDto>)>(([], []));
        }
    }
}
