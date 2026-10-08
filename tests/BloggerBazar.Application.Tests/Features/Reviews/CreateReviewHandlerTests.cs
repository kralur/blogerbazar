using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using static BloggerBazar.Application.Tests.Features.Deals.DealTestData;

namespace BloggerBazar.Application.Tests.Features.Reviews;

public sealed class CreateReviewHandlerTests
{
    [Fact]
    public async Task Selected_blogger_reviews_the_business_after_completion()
    {
        var blogger = Blogger(101, "Madina");
        var business = Business(202, "Lumi");
        var deal = CompletedDeal(blogger, business);
        var reviews = new InMemoryReviewRepository();
        var bot = new SpyBotClient();
        var handler = Handler(deal, User(101, MarketplaceRole.Blogger), blogger, business, reviews, new SpyUnitOfWork(), bot);

        var result = await handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, " Отличная коммуникация "), CancellationToken.None);

        Assert.Equal((int)ReviewTargetType.Business, result.TargetType);
        Assert.Equal(5, result.Rating);
        Assert.Equal("Отличная коммуникация", result.Comment);
        Assert.Equal("Madina", result.ReviewerName);
        var review = Assert.Single(reviews.Reviews);
        Assert.Equal(business.Id, review.BusinessId);
        Assert.Equal([business.TelegramUserId], bot.NotifiedChats);
    }

    [Fact]
    public async Task Reviewer_side_follows_the_selected_role_when_the_user_has_both_profiles()
    {
        var counterpartyBlogger = Blogger(101, "Madina");
        var ownBlogger = Blogger(303, "Own blogger");
        var ownBusiness = Business(303, "Own business");
        var deal = CompletedDeal(counterpartyBlogger, ownBusiness);
        var reviews = new InMemoryReviewRepository();
        var handler = new CreateReviewHandler(
            new FakeDeals(deal),
            new FakeUsers(User(303, MarketplaceRole.Business)),
            new FakeBloggers(counterpartyBlogger, ownBlogger),
            new FakeBusinesses(ownBusiness),
            reviews,
            new SpyUnitOfWork());

        var result = await handler.Handle(new CreateReviewCommand(deal.Id, 303, 4, null), CancellationToken.None);

        Assert.Equal((int)ReviewTargetType.Blogger, result.TargetType);
        Assert.Equal("Own business", result.ReviewerName);
        Assert.Equal(counterpartyBlogger.Id, Assert.Single(reviews.Reviews).BloggerId);
    }

    [Fact]
    public async Task Rejects_review_before_deal_completion()
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var deal = CampaignDeal(blogger, business);
        var handler = Handler(deal, User(101, MarketplaceRole.Blogger), blogger, business);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, null), CancellationToken.None));

        Assert.Contains("completed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Foreign_deal_is_missing_regardless_of_its_status()
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var outsider = Blogger(404);
        var active = CampaignDeal(blogger, business);
        var completed = CompletedDeal(blogger, business);

        foreach (var deal in new[] { active, completed })
        {
            var handler = Handler(deal, User(404, MarketplaceRole.Blogger), outsider, business);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateReviewCommand(deal.Id, 404, 5, null), CancellationToken.None));

            Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(MarketplaceRole.BrandFace)]
    [InlineData(MarketplaceRole.Business)]
    public async Task Participant_with_another_selected_role_cannot_review(MarketplaceRole role)
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var deal = CompletedDeal(blogger, business);
        var reviews = new InMemoryReviewRepository();
        var handler = Handler(deal, User(101, role), blogger, business, reviews);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, null), CancellationToken.None));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(reviews.Reviews);
    }

    [Fact]
    public async Task Existing_review_is_a_conflict()
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var deal = CompletedDeal(blogger, business);
        var reviews = new InMemoryReviewRepository { AlreadyReviewed = true };
        var handler = Handler(deal, User(101, MarketplaceRole.Blogger), blogger, business, reviews);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, null), CancellationToken.None));

        Assert.Contains("already reviewed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(reviews.Reviews);
    }

    [Fact]
    public async Task Concurrent_duplicate_review_is_a_conflict_without_notification()
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var deal = CompletedDeal(blogger, business);
        var bot = new SpyBotClient();
        var handler = Handler(deal, User(101, MarketplaceRole.Blogger), blogger, business, new InMemoryReviewRepository(), new SpyUnitOfWork(uniqueConflict: true), bot);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, null), CancellationToken.None));

        Assert.Contains("already reviewed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(bot.NotifiedChats);
    }

    [Fact]
    public async Task First_review_stays_hidden_and_invites_the_partner()
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var deal = CompletedDeal(blogger, business);
        var reviews = new InMemoryReviewRepository();
        var bot = new SpyBotClient();
        var handler = Handler(deal, User(101, MarketplaceRole.Blogger), blogger, business, reviews, new SpyUnitOfWork(), bot);

        await handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, null), CancellationToken.None);

        Assert.Null(Assert.Single(reviews.Reviews).PublishedAtUtc);
        Assert.Equal([deal.Id], reviews.PublishedDeals);
        var text = Assert.Single(bot.Texts);
        Assert.Contains("Оставьте и свой отзыв", text);
        Assert.StartsWith(blogger.Name, text);
        Assert.Equal($"/deal/{deal.Id}", Assert.Single(bot.Routes));
        Assert.Equal("Оставить отзыв", Assert.Single(bot.Buttons));
    }

    [Fact]
    public async Task Second_review_reveals_both_reviews()
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var deal = CompletedDeal(blogger, business);
        var bot = new SpyBotClient();
        var handler = Handler(deal, User(202, MarketplaceRole.Business), blogger, business, new InMemoryReviewRepository { PartnerReviewed = true }, new SpyUnitOfWork(), bot);

        await handler.Handle(new CreateReviewCommand(deal.Id, 202, 4, null), CancellationToken.None);

        Assert.Equal([blogger.TelegramUserId], bot.NotifiedChats);
        Assert.Contains("опубликованы", Assert.Single(bot.Texts));
        Assert.Equal("Посмотреть отзывы", Assert.Single(bot.Buttons));
    }

    [Fact]
    public async Task Review_after_the_window_is_a_conflict()
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var deal = CompletedDeal(blogger, business);
        Set(deal, nameof(Deal.CompletedAtUtc), (DateTime?)DateTime.UtcNow.AddDays(-14).AddMinutes(-1));
        var reviews = new InMemoryReviewRepository();
        var handler = Handler(deal, User(101, MarketplaceRole.Blogger), blogger, business, reviews);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, null), CancellationToken.None));

        Assert.Contains("window", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(reviews.Reviews);
    }

    [Fact]
    public async Task Review_on_the_last_day_of_the_window_is_accepted()
    {
        var blogger = Blogger(101);
        var business = Business(202);
        var deal = CompletedDeal(blogger, business);
        Set(deal, nameof(Deal.CompletedAtUtc), (DateTime?)DateTime.UtcNow.AddDays(-13).AddHours(-23));
        var reviews = new InMemoryReviewRepository();

        await Handler(deal, User(101, MarketplaceRole.Blogger), blogger, business, reviews).Handle(new CreateReviewCommand(deal.Id, 101, 5, null), CancellationToken.None);

        Assert.Single(reviews.Reviews);
    }

    private static Deal CompletedDeal(BloggerProfile blogger, BusinessProfile business)
    {
        var deal = CampaignDeal(blogger, business);
        deal.Complete();
        return deal;
    }

    private static CreateReviewHandler Handler(
        Deal deal,
        PlatformUser user,
        BloggerProfile blogger,
        BusinessProfile business,
        InMemoryReviewRepository? reviews = null,
        SpyUnitOfWork? unitOfWork = null,
        SpyBotClient? bot = null) =>
        new(new FakeDeals(deal), new FakeUsers(user), new FakeBloggers(blogger), new FakeBusinesses(business), reviews ?? new InMemoryReviewRepository(), unitOfWork ?? new SpyUnitOfWork(), bot);

    private sealed class InMemoryReviewRepository : IReviewRepository
    {
        public bool AlreadyReviewed { get; init; }
        public List<Review> Reviews { get; } = [];

        public Task AddAsync(Review review, CancellationToken cancellationToken)
        {
            Reviews.Add(review);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken) => Task.FromResult(AlreadyReviewed);

        // Simulates the partner review: when present, publishing reveals both reviews.
        public bool PartnerReviewed { get; init; }
        public List<Guid?> PublishedDeals { get; } = [];

        public Task<int> PublishRevealedAsync(Guid? dealId, DateTime nowUtc, CancellationToken cancellationToken)
        {
            PublishedDeals.Add(dealId);
            return Task.FromResult(PartnerReviewed ? 2 : 0);
        }
    }
}
