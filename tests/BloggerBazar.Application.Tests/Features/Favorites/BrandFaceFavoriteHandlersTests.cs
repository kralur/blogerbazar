using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Favorites;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Favorites;

public sealed class BrandFaceFavoriteHandlersTests
{
    [Fact]
    public async Task Business_can_save_a_public_brand_face_and_repeated_save_is_idempotent()
    {
        var user = BusinessUser();
        var brandFace = BrandFaceProfile.Create(20, "Dilnoza", "tashkent-city", ["beauty"]);
        var favorites = new InMemoryFavorites();
        var audits = new InMemoryAuditLogs();
        var handler = new SaveBrandFaceFavoriteCommandHandler(new InMemoryUsers(user), new InMemoryBrandFaces(brandFace), favorites, audits, new UnitOfWork());

        var first = await handler.Handle(new SaveBrandFaceFavoriteCommand(user.TelegramUserId, brandFace.Id), CancellationToken.None);
        var repeated = await handler.Handle(new SaveBrandFaceFavoriteCommand(user.TelegramUserId, brandFace.Id), CancellationToken.None);

        Assert.True(first.IsFavorite);
        Assert.True(repeated.IsFavorite);
        Assert.Single(favorites.Items);
        Assert.Single(audits.Entries);
    }

    [Theory]
    [InlineData(MarketplaceRole.Blogger)]
    [InlineData(MarketplaceRole.BrandFace)]
    public async Task Non_business_roles_cannot_manage_brand_face_favorites(MarketplaceRole role)
    {
        var user = PlatformUser.Create(10, "User", "user");
        user.SelectMarketplaceRole(role);
        var brandFace = BrandFaceProfile.Create(20, "Dilnoza", "tashkent-city", ["beauty"]);
        var handler = new SaveBrandFaceFavoriteCommandHandler(new InMemoryUsers(user), new InMemoryBrandFaces(brandFace), new InMemoryFavorites(), new InMemoryAuditLogs(), new UnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new SaveBrandFaceFavoriteCommand(user.TelegramUserId, brandFace.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Deleted_or_blocked_brand_face_cannot_be_saved()
    {
        var user = BusinessUser();
        var handler = new SaveBrandFaceFavoriteCommandHandler(new InMemoryUsers(user), new InMemoryBrandFaces(null), new InMemoryFavorites(), new InMemoryAuditLogs(), new UnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new SaveBrandFaceFavoriteCommand(user.TelegramUserId, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Repeated_delete_is_idempotent()
    {
        var user = BusinessUser();
        var handler = new RemoveBrandFaceFavoriteCommandHandler(new InMemoryUsers(user), new InMemoryFavorites(), new InMemoryAuditLogs(), new UnitOfWork());

        var result = await handler.Handle(new RemoveBrandFaceFavoriteCommand(user.TelegramUserId, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsFavorite);
    }

    [Fact]
    public void Favorite_dto_does_not_expose_contact_fields()
    {
        var properties = typeof(BrandFaceFavoriteDto).GetProperties().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Telegram", properties);
        Assert.DoesNotContain("Instagram", properties);
        Assert.DoesNotContain("PortfolioUrl", properties);
        Assert.DoesNotContain("Description", properties);
    }

    private static PlatformUser BusinessUser()
    {
        var user = PlatformUser.Create(10, "Business", "business");
        user.SelectMarketplaceRole(MarketplaceRole.Business);
        return user;
    }

    private sealed class InMemoryUsers(params PlatformUser[] users) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([]);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryBrandFaces(BrandFaceProfile? profile) : IBrandFaceProfileRepository
    {
        public Task<BrandFaceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profile?.Id == id ? profile : null);
        public Task<BrandFaceProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BrandFaceProfile?>(null);
        public Task<IReadOnlyList<BrandFaceProfile>> GetAllAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>([]);
        public Task<IReadOnlyList<BrandFaceProfile>> SearchAsync(string? query, string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>([]);
        public Task AddAsync(BrandFaceProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryFavorites : IBrandFaceFavoriteRepository
    {
        public List<BrandFaceFavorite> Items { get; } = [];
        public Task<BrandFaceFavorite?> GetAsync(Guid platformUserId, Guid brandFaceId, CancellationToken cancellationToken) => Task.FromResult(Items.SingleOrDefault(item => item.PlatformUserId == platformUserId && item.BrandFaceId == brandFaceId));
        public Task AddAsync(BrandFaceFavorite favorite, CancellationToken cancellationToken) { Items.Add(favorite); return Task.CompletedTask; }
        public Task<bool> DeleteAsync(Guid platformUserId, Guid brandFaceId, CancellationToken cancellationToken)
        {
            var item = Items.SingleOrDefault(item => item.PlatformUserId == platformUserId && item.BrandFaceId == brandFaceId);
            return Task.FromResult(item is not null && Items.Remove(item));
        }
        public Task DeleteForPlatformUserAsync(Guid platformUserId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteForBrandFaceAsync(Guid brandFaceId, CancellationToken cancellationToken) => Task.CompletedTask;
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
