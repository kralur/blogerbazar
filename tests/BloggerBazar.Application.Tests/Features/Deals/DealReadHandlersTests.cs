using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Deals;

public sealed class DealReadHandlersTests
{
    [Fact]
    public async Task List_queries_only_the_selected_role_profile()
    {
        var ownBlogger = Blogger(11);
        var ownBusiness = Business(11);
        var readModel = new FakeDealReadModel(Row());
        var handler = new GetMyDealsHandler(new FakeUsers(User(11, MarketplaceRole.Business)), new FakeBloggers(ownBlogger), new FakeBusinesses(ownBusiness), readModel);

        await handler.Handle(new GetMyDealsQuery(11), CancellationToken.None);

        Assert.Equal((MarketplaceRole.Business, ownBusiness.Id), Assert.Single(readModel.Queries));
    }

    [Theory]
    [InlineData(MarketplaceRole.BrandFace)]
    [InlineData(MarketplaceRole.Business)]
    [InlineData(null)]
    public async Task List_is_empty_without_a_deal_side_for_the_selected_role(MarketplaceRole? role)
    {
        var readModel = new FakeDealReadModel(Row());
        var handler = new GetMyDealsHandler(new FakeUsers(User(11, role)), new FakeBloggers(Blogger(11)), new FakeBusinesses(), readModel);

        var result = await handler.Handle(new GetMyDealsQuery(11), CancellationToken.None);

        Assert.Empty(result);
        Assert.Empty(readModel.Queries);
    }

    [Fact]
    public async Task List_uses_snapshot_title_and_counterparty_of_the_selected_side()
    {
        var row = Row() with { SnapshotVersion = 1, SnapshotTitle = "Agreed title", LiveCampaignTitle = "Edited title" };
        var asBlogger = await List(row, MarketplaceRole.Blogger);
        var asBusiness = await List(row, MarketplaceRole.Business);

        Assert.Equal("Agreed title", asBlogger.Title);
        Assert.Equal(DealSourceTypes.CampaignApplication, asBlogger.SourceType);
        Assert.Equal(DealTermsSources.Snapshot, asBlogger.TermsSource);
        Assert.Equal("Business name", asBlogger.CounterpartyName);
        Assert.Equal("business.png", asBlogger.CounterpartyImageUrl);
        Assert.Equal("Blogger name", asBusiness.CounterpartyName);
        Assert.Equal("blogger.png", asBusiness.CounterpartyImageUrl);
    }

    [Fact]
    public async Task List_marks_historical_campaign_deal_as_live_fallback()
    {
        var item = await List(Row() with { LiveCampaignTitle = "Current title" }, MarketplaceRole.Blogger);

        Assert.Equal("Current title", item.Title);
        Assert.Equal(DealTermsSources.LiveCampaignFallback, item.TermsSource);
    }

    [Fact]
    public async Task List_keeps_legacy_title_for_collaboration_deals_and_adds_structured_source()
    {
        var item = await List(Row() with { CampaignApplicationId = null, CollaborationRequestId = Guid.NewGuid() }, MarketplaceRole.Business);

        Assert.Equal("Direct collaboration request", item.Title);
        Assert.Equal(DealSourceTypes.CollaborationRequest, item.SourceType);
        Assert.Equal(DealTermsSources.Collaboration, item.TermsSource);
    }

    [Fact]
    public async Task Review_availability_depends_on_the_viewer_side()
    {
        var row = Row() with { Status = DealStatus.Completed, CompletedAtUtc = DateTime.UtcNow, BloggerHasReviewed = true };

        var asBlogger = await List(row, MarketplaceRole.Blogger);
        var asBusiness = await List(row, MarketplaceRole.Business);

        Assert.False(asBlogger.CanComplete);
        Assert.False(asBlogger.CanReview);
        Assert.True(asBusiness.CanReview);
    }

    [Fact]
    public async Task Review_closes_fourteen_days_after_completion()
    {
        var completedAt = DateTime.UtcNow.AddDays(-14).AddMinutes(-1);
        var row = Row() with { Status = DealStatus.Completed, CompletedAtUtc = completedAt };

        var listed = await List(row, MarketplaceRole.Business);
        var details = await Details(row, MarketplaceRole.Business);

        Assert.False(listed.CanReview);
        Assert.False(details.CanReview);
        Assert.False(details.HasReviewed);
        Assert.Equal(completedAt.AddDays(14), details.ReviewDeadlineUtc);
    }

    [Fact]
    public async Task Active_deal_has_no_review_deadline()
    {
        var details = await Details(Row(), MarketplaceRole.Blogger);

        Assert.Null(details.ReviewDeadlineUtc);
    }

    [Fact]
    public async Task Details_return_snapshot_terms()
    {
        var deadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var row = Row() with
        {
            SnapshotVersion = 1,
            SnapshotTitle = "Agreed title",
            SnapshotDescription = "Agreed description",
            SnapshotCity = "tashkent",
            SnapshotCategories = ["beauty"],
            SnapshotRequirements = ["One reel"],
            SnapshotBudgetFrom = 100,
            SnapshotBudgetTo = 200,
            SnapshotDeadline = deadline,
            LiveCampaignTitle = "Edited title"
        };

        var details = await Details(row, MarketplaceRole.Business);

        Assert.Equal(DealTermsSources.Snapshot, details.TermsSource);
        var terms = Assert.IsType<DealTermsDto>(details.Terms);
        Assert.Equal("Agreed title", terms.Title);
        Assert.Equal("Agreed description", terms.Description);
        Assert.Equal("tashkent", terms.City);
        Assert.Equal(100, terms.BudgetFrom);
        Assert.Equal(200, terms.BudgetTo);
        Assert.Equal(deadline, terms.Deadline);
        Assert.Equal(["beauty"], terms.Categories);
        Assert.Equal(["One reel"], terms.Requirements);
        Assert.True(details.CanComplete);
        Assert.False(details.HasReviewed);
    }

    [Fact]
    public async Task Details_of_collaboration_deal_have_no_campaign_terms()
    {
        var details = await Details(Row() with { CampaignApplicationId = null, CollaborationRequestId = Guid.NewGuid() }, MarketplaceRole.Blogger);

        Assert.Equal(DealSourceTypes.CollaborationRequest, details.SourceType);
        Assert.Equal(DealTermsSources.Collaboration, details.TermsSource);
        Assert.Null(details.Terms);
    }

    [Fact]
    public async Task Details_of_an_offer_deal_include_the_accepted_offer_terms()
    {
        var deadline = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var row = Row() with
        {
            CampaignApplicationId = null,
            CollaborationRequestId = Guid.NewGuid(),
            OfferFormat = CollaborationFormat.Reels,
            OfferedBudget = 1_500_000,
            OfferDeadline = deadline,
            OfferMessage = "One reel"
        };

        var details = await Details(row, MarketplaceRole.Blogger);

        Assert.Equal(new DealOfferDto("reels", 1_500_000, deadline, "One reel"), details.Offer);
        Assert.Null(details.Terms);
    }

    [Fact]
    public async Task Details_of_foreign_or_missing_deal_are_not_found()
    {
        var handler = new GetMyDealHandler(new FakeUsers(User(11, MarketplaceRole.Blogger)), new FakeBloggers(Blogger(11)), new FakeBusinesses(), new FakeDealReadModel());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new GetMyDealQuery(Guid.NewGuid(), 11), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Details_for_brand_face_role_are_not_found()
    {
        var row = Row();
        var readModel = new FakeDealReadModel(row);
        var handler = new GetMyDealHandler(new FakeUsers(User(11, MarketplaceRole.BrandFace)), new FakeBloggers(Blogger(11)), new FakeBusinesses(), readModel);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new GetMyDealQuery(row.Id, 11), CancellationToken.None));

        Assert.Empty(readModel.Queries);
    }

    private static async Task<MyDealDto> List(DealReadRow row, MarketplaceRole role)
    {
        var handler = new GetMyDealsHandler(new FakeUsers(User(11, role)), new FakeBloggers(Blogger(11)), new FakeBusinesses(Business(11)), new FakeDealReadModel(row));
        return Assert.Single(await handler.Handle(new GetMyDealsQuery(11), CancellationToken.None));
    }

    private static Task<DealDetailsDto> Details(DealReadRow row, MarketplaceRole role)
    {
        var handler = new GetMyDealHandler(new FakeUsers(User(11, role)), new FakeBloggers(Blogger(11)), new FakeBusinesses(Business(11)), new FakeDealReadModel(row));
        return handler.Handle(new GetMyDealQuery(row.Id, 11), CancellationToken.None);
    }

    private static DealReadRow Row() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        null,
        DealStatus.Active,
        DateTime.UtcNow,
        null,
        "Blogger name",
        "blogger.png",
        "Business name",
        "business.png",
        null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null,
        false,
        false);
}
