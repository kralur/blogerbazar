using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Payments;

public sealed class GetUnlockedContactHandlerTests
{
    [Fact]
    public async Task Rejects_viewer_without_unlock()
    {
        var target = CreateBlogger();
        var handler = new GetUnlockedContactHandler(new InMemoryBloggerRepository(target), new InMemoryBusinessRepository(), new InMemoryContactUnlockRepository());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetUnlockedContactQuery(101, ContactTargetType.Blogger, target.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Returns_contact_to_profile_owner()
    {
        var target = CreateBlogger();
        var handler = new GetUnlockedContactHandler(new InMemoryBloggerRepository(target), new InMemoryBusinessRepository(), new InMemoryContactUnlockRepository());

        var result = await handler.Handle(new GetUnlockedContactQuery(202, ContactTargetType.Blogger, target.Id), CancellationToken.None);

        Assert.Equal("+998901234567", result.Phone);
        Assert.Equal("madina@example.com", result.Email);
    }

    private static BloggerProfile CreateBlogger()
    {
        var profile = BloggerProfile.Create(202, "Madina", "Tashkent", ["Lifestyle"]);
        profile.UpdatePublicProfile("Madina", null, null, "Tashkent", ["Lifestyle"], null, null, "+998901234567", "madina@example.com", 1000, null, null, null, null, null, null, false);
        return profile;
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile target) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == target.Id ? target : null);
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
