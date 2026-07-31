using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Favorites;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Favorites;

public sealed class FavoriteHandlersTests
{
    [Fact]
    public async Task Business_can_save_a_blogger_and_repeated_save_is_idempotent()
    {
        var user = PlatformUser.Create(10, "Business", "business");
        user.SelectMarketplaceRole(MarketplaceRole.Business);
        var blogger = BloggerProfile.Create(20, "Blogger", "tashkent", ["beauty"]);
        blogger.Approve();
        var favorites = new InMemoryFavorites();
        var audits = new InMemoryAuditLogs();
        var handler = new SaveFavoriteCommandHandler(new InMemoryUsers(user), new InMemoryBloggers(blogger), favorites, audits, new UnitOfWork());

        var first = await handler.Handle(new SaveFavoriteCommand(10, blogger.Id), CancellationToken.None);
        var repeated = await handler.Handle(new SaveFavoriteCommand(10, blogger.Id), CancellationToken.None);

        Assert.True(first.IsFavorite);
        Assert.True(repeated.IsFavorite);
        Assert.Single(favorites.Items);
        Assert.Single(audits.Entries);
    }

    [Fact]
    public async Task Blogger_cannot_save_favorites()
    {
        var user = PlatformUser.Create(10, "Blogger", "blogger");
        user.SelectMarketplaceRole(MarketplaceRole.Blogger);
        var blogger = BloggerProfile.Create(20, "Another", "tashkent", ["beauty"]);
        var handler = new SaveFavoriteCommandHandler(new InMemoryUsers(user), new InMemoryBloggers(blogger), new InMemoryFavorites(), new InMemoryAuditLogs(), new UnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new SaveFavoriteCommand(10, blogger.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Cannot_save_own_blogger_profile()
    {
        var user = PlatformUser.Create(10, "Business", "business");
        user.SelectMarketplaceRole(MarketplaceRole.Business);
        var blogger = BloggerProfile.Create(10, "Same person", "tashkent", ["beauty"]);
        blogger.Approve();
        var handler = new SaveFavoriteCommandHandler(new InMemoryUsers(user), new InMemoryBloggers(blogger), new InMemoryFavorites(), new InMemoryAuditLogs(), new UnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new SaveFavoriteCommand(10, blogger.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Removing_an_absent_favorite_is_idempotent()
    {
        var user = PlatformUser.Create(10, "Business", "business");
        user.SelectMarketplaceRole(MarketplaceRole.Business);
        var handler = new RemoveFavoriteCommandHandler(new InMemoryUsers(user), new InMemoryFavorites(), new InMemoryAuditLogs(), new UnitOfWork());

        var result = await handler.Handle(new RemoveFavoriteCommand(10, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsFavorite);
    }

    private sealed class InMemoryUsers(params PlatformUser[] users) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([]);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryBloggers(BloggerProfile blogger) : IBloggerProfileRepository
    {
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(blogger.Id == id && !blogger.IsDeleted ? blogger : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryFavorites : IFavoriteRepository
    {
        public List<Favorite> Items { get; } = [];
        public Task<Favorite?> GetAsync(Guid platformUserId, Guid bloggerId, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(item => item.PlatformUserId == platformUserId && item.BloggerId == bloggerId));
        public Task AddAsync(Favorite favorite, CancellationToken cancellationToken) { Items.Add(favorite); return Task.CompletedTask; }
        public Task<bool> DeleteAsync(Guid platformUserId, Guid bloggerId, CancellationToken cancellationToken)
        {
            var item = Items.SingleOrDefault(item => item.PlatformUserId == platformUserId && item.BloggerId == bloggerId);
            return Task.FromResult(item is not null && Items.Remove(item));
        }
        public Task DeleteForPlatformUserAsync(Guid platformUserId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryAuditLogs : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>([]);
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
