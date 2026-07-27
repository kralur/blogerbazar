using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Bloggers;

public sealed class CreateBloggerProfileHandlerTests
{
    [Fact]
    public async Task Creates_pending_profile_and_persists_it()
    {
        var repository = new InMemoryBloggerRepository();
        var unitOfWork = new SpyUnitOfWork();
        var handler = new CreateBloggerProfileHandler(repository, new InMemoryPortfolioRepository(), new InMemorySocialPlatformRepository(), unitOfWork);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.Equal("Madina", result.Name);
        Assert.Equal("Ташкент", result.City);
        Assert.Equal(1, unitOfWork.SaveCallCount);
        Assert.Single(repository.Profiles);
        Assert.Equal(0, result.Status);
    }

    [Fact]
    public async Task Rejects_second_profile_for_the_same_telegram_user()
    {
        var repository = new InMemoryBloggerRepository();
        var unitOfWork = new SpyUnitOfWork();
        var handler = new CreateBloggerProfileHandler(repository, new InMemoryPortfolioRepository(), new InMemorySocialPlatformRepository(), unitOfWork);
        await handler.Handle(CreateCommand(), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, unitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task Persists_valid_portfolio_items_with_profile()
    {
        var repository = new InMemoryBloggerRepository();
        var portfolio = new InMemoryPortfolioRepository();
        var handler = new CreateBloggerProfileHandler(repository, portfolio, new InMemorySocialPlatformRepository(), new SpyUnitOfWork());
        var command = CreateCommand() with
        {
            PortfolioItems = [new PortfolioItemInput("Brand launch", PortfolioItemType.Video, "https://example.com/work.mp4")]
        };

        await handler.Handle(command, CancellationToken.None);

        Assert.Single(portfolio.Items);
        Assert.Equal("Brand launch", portfolio.Items[0].Title);
    }

    private static CreateBloggerProfileCommand CreateCommand() => new(
        123456789,
        "Madina",
        "Karimova",
        "madina.karimova",
        "Ташкент",
        ["Красота", "Lifestyle"],
        "Beauty creator",
        "https://example.com/avatar.jpg",
        52000,
        112000,
        9.1m,
        350000,
        550000,
        450000,
        1500000,
        true);

    private sealed class InMemoryBloggerRepository : IBloggerProfileRepository
    {
        public List<BloggerProfile> Profiles { get; } = [];

        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken)
        {
            Profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Profiles.SingleOrDefault(profile => profile.Id == id));

        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(Profiles.SingleOrDefault(profile => profile.TelegramUserId == telegramUserId));

        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public int SaveCallCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCallCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class InMemoryPortfolioRepository : IPortfolioItemRepository
    {
        public List<PortfolioItem> Items { get; } = [];

        public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken)
        {
            Items.RemoveAll(item => item.BloggerId == bloggerId);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<PortfolioItem> portfolioItems, CancellationToken cancellationToken)
        {
            Items.AddRange(portfolioItems);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySocialPlatformRepository : ISocialPlatformRepository
    {
        public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<SocialPlatform> platforms, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
