using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Bloggers;

public sealed class UpdateBloggerProfileHandlerTests
{
    [Fact]
    public async Task Replaces_portfolio_and_updates_creator_metrics()
    {
        var profile = BloggerProfile.Create(101, "Madina", "Tashkent", ["Lifestyle"]);
        var portfolios = new InMemoryPortfolioRepository();
        await portfolios.AddRangeAsync([PortfolioItem.Create(profile.Id, "Old work", PortfolioItemType.Image, "https://cdn.example/old.jpg")], CancellationToken.None);
        var handler = new UpdateBloggerProfileHandler(new InMemoryBloggerRepository(profile), portfolios, new InMemorySocialPlatformRepository(), new SpyUnitOfWork());

        var result = await handler.Handle(new UpdateBloggerProfileCommand(
            101, "Madina", "Karimova", "@madina", "Samarkand", ["Beauty"], "Beauty creator", null,
            52000, 12000, 8.4m, 350000, 550000, 450000, 900000, true, "+998901234567", "madina@example.uz",
            [new PortfolioItemInput("New work", PortfolioItemType.Video, "https://cdn.example/new.mp4")]), CancellationToken.None);

        Assert.Equal("Samarkand", result.City);
        Assert.Equal(52000, result.TotalFollowers);
        Assert.Single(portfolios.Items);
        Assert.Equal("New work", portfolios.Items[0].Title);
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile profile) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == profile.Id ? profile : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(telegramUserId == profile.TelegramUserId ? profile : null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class InMemoryPortfolioRepository : IPortfolioItemRepository
    {
        public List<PortfolioItem> Items { get; } = [];
        public Task AddRangeAsync(IEnumerable<PortfolioItem> portfolioItems, CancellationToken cancellationToken) { Items.AddRange(portfolioItems); return Task.CompletedTask; }
        public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) { Items.RemoveAll(item => item.BloggerId == bloggerId); return Task.CompletedTask; }
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class InMemorySocialPlatformRepository : ISocialPlatformRepository
    {
        public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<SocialPlatform> platforms, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
