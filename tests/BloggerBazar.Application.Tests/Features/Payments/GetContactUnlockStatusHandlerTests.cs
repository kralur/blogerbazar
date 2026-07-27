using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Payments;

public sealed class GetContactUnlockStatusHandlerTests
{
    [Fact]
    public async Task Treats_the_contact_owner_as_unlocked()
    {
        var blogger = BloggerProfile.Create(101, "Madina", "Tashkent", ["Lifestyle"]);
        var handler = new GetContactUnlockStatusHandler(
            new InMemoryBloggerRepository(blogger),
            new InMemoryBusinessRepository(),
            new InMemoryContactUnlockRepository());

        var result = await handler.Handle(
            new GetContactUnlockStatusQuery(blogger.TelegramUserId, ContactTargetType.Blogger, blogger.Id),
            CancellationToken.None);

        Assert.True(result.IsUnlocked);
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile blogger) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == blogger.Id ? blogger : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class InMemoryBusinessRepository : IBusinessProfileRepository
    {
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BusinessProfile?>(null);
    }

    private sealed class InMemoryContactUnlockRepository : IContactUnlockRepository
    {
        public Task AddAsync(ContactUnlock contactUnlock, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ContactUnlock?> GetAsync(long viewerTelegramUserId, ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken) => Task.FromResult<ContactUnlock?>(null);
    }
}
