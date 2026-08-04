using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Payments;

public sealed class GetUnlockedContactHandlerTests
{
    [Fact]
    public async Task Returns_public_contact()
    {
        var target = CreateBlogger();
        var handler = new GetUnlockedContactHandler(new InMemoryBloggerRepository(target), new InMemoryBusinessRepository());

        var result = await handler.Handle(new GetUnlockedContactQuery(ContactTargetType.Blogger, target.Id), CancellationToken.None);

        Assert.Equal("+998901234567", result.Phone);
        Assert.Equal("madina@example.com", result.Email);
        Assert.Equal("@madina", result.Telegram);
    }

    [Fact]
    public async Task Returns_same_public_contact_for_repeated_queries()
    {
        var target = CreateBlogger();
        var handler = new GetUnlockedContactHandler(new InMemoryBloggerRepository(target), new InMemoryBusinessRepository());

        var result = await handler.Handle(new GetUnlockedContactQuery(ContactTargetType.Blogger, target.Id), CancellationToken.None);

        Assert.Equal("+998901234567", result.Phone);
        Assert.Equal("madina@example.com", result.Email);
    }

    private static BloggerProfile CreateBlogger()
    {
        var profile = BloggerProfile.Create(202, "Madina", "Tashkent", ["Lifestyle"]);
        profile.UpdatePublicProfile("Madina", null, "@madina", "Tashkent", ["Lifestyle"], null, null, "+998901234567", "madina@example.com", 1000, null, null, null, null, null, null, false);
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
}
