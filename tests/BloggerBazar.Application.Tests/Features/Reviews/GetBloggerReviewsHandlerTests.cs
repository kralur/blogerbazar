using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Reviews;

public sealed class GetBloggerReviewsHandlerTests
{
    [Fact]
    public async Task Returns_public_review_fields_for_blogger()
    {
        var bloggerId = Guid.NewGuid();
        var review = Review.ForBlogger(Guid.NewGuid(), 100, bloggerId, 5, "Reliable partner");
        var handler = new GetBloggerReviewsHandler(new InMemoryReviewRepository(review));

        var result = await handler.Handle(new GetBloggerReviewsQuery(bloggerId), CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal(5, item.Rating);
        Assert.Equal("Reliable partner", item.Comment);
    }

    private sealed class InMemoryReviewRepository(params Review[] reviews) : IReviewRepository
    {
        public Task AddAsync(Review review, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<Review>> GetForBloggerAsync(Guid bloggerId, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Review>>(reviews.Where(review => review.BloggerId == bloggerId).Take(take).ToArray());
    }
}
