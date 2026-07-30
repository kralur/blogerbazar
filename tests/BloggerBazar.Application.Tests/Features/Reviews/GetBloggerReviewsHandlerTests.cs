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

    private sealed class InMemoryReviewReadModel(params ReviewDto[] reviews) : IReviewReadModel
    {
        public Task<IReadOnlyList<ReviewDto>> GetBloggerReviewsAsync(Guid bloggerId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReviewDto>>(reviews.Take(take).ToArray());
    }
}
