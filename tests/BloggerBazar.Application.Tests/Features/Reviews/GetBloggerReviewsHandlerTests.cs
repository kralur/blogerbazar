using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;

namespace BloggerBazar.Application.Tests.Features.Reviews;

public sealed class GetBloggerReviewsHandlerTests
{
    [Fact]
    public async Task Returns_public_review_fields_for_blogger()
    {
        var bloggerId = Guid.NewGuid();
        var review = new ReviewDto(Guid.NewGuid(), Guid.NewGuid(), 0, 5, "Reliable partner", "Business", DateTime.UtcNow);
        var handler = new GetBloggerReviewsHandler(new InMemoryReviewReadModel(review));

        var result = await handler.Handle(new GetBloggerReviewsQuery(bloggerId), CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(5, item.Rating);
        Assert.Equal("Reliable partner", item.Comment);
    }

    [Fact]
    public async Task Skips_reviews_already_shown()
    {
        var newest = new ReviewDto(Guid.NewGuid(), Guid.NewGuid(), 0, 5, "Newest", "Business", DateTime.UtcNow);
        var older = new ReviewDto(Guid.NewGuid(), Guid.NewGuid(), 0, 4, "Older", "Business", DateTime.UtcNow.AddDays(-1));
        var handler = new GetBloggerReviewsHandler(new InMemoryReviewReadModel(newest, older));

        var result = await handler.Handle(new GetBloggerReviewsQuery(Guid.NewGuid(), Take: 10, Skip: 1), CancellationToken.None);

        Assert.Equal("Older", Assert.Single(result).Comment);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Rejects_a_skip_outside_the_allowed_range(int skip)
    {
        var result = new GetBloggerReviewsValidator().Validate(new GetBloggerReviewsQuery(Guid.NewGuid(), Skip: skip));

        Assert.False(result.IsValid);
    }

    private sealed class InMemoryReviewReadModel(params ReviewDto[] reviews) : IReviewReadModel
    {
        public Task<IReadOnlyList<ReviewDto>> GetBloggerReviewsAsync(Guid bloggerId, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReviewDto>>(reviews.Skip(skip).Take(take).ToArray());

        public Task<BusinessReviewsDto> GetBusinessReviewsAsync(Guid businessId, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(new BusinessReviewsDto(null, 0, []));
    }
}
