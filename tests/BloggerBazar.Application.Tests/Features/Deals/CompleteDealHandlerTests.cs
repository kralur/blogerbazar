using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Deals;

public sealed class CompleteDealHandlerTests
{
    [Fact]
    public async Task Completes_an_active_deal_for_a_participant()
    {
        var blogger = BloggerProfile.Create(11, "Madina", "Tashkent", ["Lifestyle"]);
        var business = BusinessProfile.Create(22, "Brand", "Tashkent");
        var deal = Deal.Create(Guid.NewGuid(), blogger.Id, business.Id);
        var unitOfWork = new SpyUnitOfWork();
        var handler = new CompleteDealHandler(
            new InMemoryDealRepository(deal),
            new InMemoryBloggerRepository(blogger),
            new InMemoryBusinessRepository(business),
            unitOfWork);

        var result = await handler.Handle(new CompleteDealCommand(deal.Id, blogger.TelegramUserId), CancellationToken.None);

        Assert.Equal((int)DealStatus.Completed, result.Status);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task Rejects_completion_by_a_non_participant()
    {
        var blogger = BloggerProfile.Create(11, "Madina", "Tashkent", ["Lifestyle"]);
        var business = BusinessProfile.Create(22, "Brand", "Tashkent");
        var deal = Deal.Create(Guid.NewGuid(), blogger.Id, business.Id);
        var handler = new CompleteDealHandler(
            new InMemoryDealRepository(deal),
            new InMemoryBloggerRepository(blogger),
            new InMemoryBusinessRepository(business),
            new SpyUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new CompleteDealCommand(deal.Id, 33), CancellationToken.None));

        Assert.Equal(DealStatus.Active, deal.Status);
    }

    private sealed class InMemoryDealRepository(Deal deal) : IDealRepository
    {
        public Task AddAsync(Deal value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Deal?>(id == deal.Id ? deal : null);
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile blogger) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == blogger.Id ? blogger : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(telegramUserId == blogger.TelegramUserId ? blogger : null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class InMemoryBusinessRepository(BusinessProfile business) : IBusinessProfileRepository
    {
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(id == business.Id ? business : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(telegramUserId == business.TelegramUserId ? business : null);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }
}
