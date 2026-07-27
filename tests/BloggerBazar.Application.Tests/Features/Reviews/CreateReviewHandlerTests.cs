using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Features.Reviews;

public sealed class CreateReviewHandlerTests
{
    [Fact]
    public async Task Creates_review_for_business_after_completed_deal()
    {
        var blogger = BloggerProfile.Create(101, "Madina", "Ташкент", ["Lifestyle"]);
        var business = BusinessProfile.Create(202, "Lumi", "Ташкент");
        var deal = Deal.Create(Guid.NewGuid(), blogger.Id, business.Id);
        deal.Complete();
        var reviews = new InMemoryReviewRepository();
        var handler = new CreateReviewHandler(new InMemoryDealRepository(deal), new InMemoryBloggerRepository(blogger), new InMemoryBusinessRepository(business), reviews, new SpyUnitOfWork());

        var result = await handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, "Отличная коммуникация"), CancellationToken.None);

        Assert.Equal(1, result.TargetType);
        Assert.Equal(5, result.Rating);
        Assert.Single(reviews.Reviews);
    }

    [Fact]
    public async Task Rejects_review_before_deal_completion()
    {
        var blogger = BloggerProfile.Create(101, "Madina", "Ташкент", ["Lifestyle"]);
        var business = BusinessProfile.Create(202, "Lumi", "Ташкент");
        var deal = Deal.Create(Guid.NewGuid(), blogger.Id, business.Id);
        var handler = new CreateReviewHandler(new InMemoryDealRepository(deal), new InMemoryBloggerRepository(blogger), new InMemoryBusinessRepository(business), new InMemoryReviewRepository(), new SpyUnitOfWork());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateReviewCommand(deal.Id, 101, 5, null), CancellationToken.None));

        Assert.Contains("completed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InMemoryDealRepository(Deal deal) : IDealRepository
    {
        public Task AddAsync(Deal value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Deal?>(id == deal.Id ? deal : null);
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile profile) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == profile.Id ? profile : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(telegramUserId == profile.TelegramUserId ? profile : null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class InMemoryBusinessRepository(BusinessProfile profile) : IBusinessProfileRepository
    {
        public Task AddAsync(BusinessProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(id == profile.Id ? profile : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(telegramUserId == profile.TelegramUserId ? profile : null);
    }

    private sealed class InMemoryReviewRepository : IReviewRepository
    {
        public List<Review> Reviews { get; } = [];
        public Task AddAsync(Review review, CancellationToken cancellationToken)
        {
            Reviews.Add(review);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid dealId, long reviewerTelegramUserId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
