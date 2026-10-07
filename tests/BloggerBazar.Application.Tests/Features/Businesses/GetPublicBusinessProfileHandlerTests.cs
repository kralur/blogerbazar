using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Application.Features.Reviews;

namespace BloggerBazar.Application.Tests.Features.Businesses;

public sealed class GetPublicBusinessProfileHandlerTests
{
    [Fact]
    public async Task Adds_rating_and_latest_reviews_to_the_profile()
    {
        var id = Guid.NewGuid();
        var profile = new PublicBusinessProfileDto(id, "Lumi", "tashkent", null, null, null, true, 3, DateTime.UtcNow, []);
        var review = new ReviewDto(Guid.NewGuid(), Guid.NewGuid(), 1, 5, "Хороший", "Madina", DateTime.UtcNow, Guid.NewGuid(), null);
        var handler = new GetPublicBusinessProfileHandler(new FakeBusinesses(profile), new FakeReviews(new BusinessReviewsDto(4.5m, 2, [review])));

        var result = await handler.Handle(new GetPublicBusinessProfileQuery(id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(4.5m, result.Rating);
        Assert.Equal(2, result.ReviewsCount);
        Assert.Equal(review, Assert.Single(result.Reviews!));
    }

    [Fact]
    public async Task Hidden_business_is_not_found()
    {
        var handler = new GetPublicBusinessProfileHandler(new FakeBusinesses(null), new FakeReviews(new BusinessReviewsDto(null, 0, [])));

        Assert.Null(await handler.Handle(new GetPublicBusinessProfileQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private sealed class FakeBusinesses(PublicBusinessProfileDto? profile) : IPublicBusinessReadModel
    {
        public Task<PublicBusinessProfileDto?> GetAsync(Guid businessId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(profile);
    }

    private sealed class FakeReviews(BusinessReviewsDto reviews) : IReviewReadModel
    {
        public Task<IReadOnlyList<ReviewDto>> GetBloggerReviewsAsync(Guid bloggerId, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReviewDto>>([]);
        public Task<BusinessReviewsDto> GetBusinessReviewsAsync(Guid businessId, int take, CancellationToken cancellationToken) => Task.FromResult(reviews);
    }
}
