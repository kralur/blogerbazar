using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Users;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Users;

public sealed class ConfiguredOwnerTests
{
    [Fact]
    public async Task The_configured_owner_gets_the_owner_role_on_sign_in()
    {
        var users = new Users();

        var result = await new GetCurrentPlatformUserHandler(users, new SpyUnitOfWork(), new Owners(42))
            .Handle(new GetCurrentPlatformUserCommand(42, "Umid", null), CancellationToken.None);

        Assert.Equal(PlatformRole.Owner, result.Role);
    }

    [Fact]
    public async Task Anyone_else_stays_a_member()
    {
        var users = new Users();

        var result = await new GetCurrentPlatformUserHandler(users, new SpyUnitOfWork(), new Owners(42))
            .Handle(new GetCurrentPlatformUserCommand(7, "Guest", null), CancellationToken.None);

        Assert.Equal(PlatformRole.Member, result.Role);
    }

    private sealed class Owners(params long[] ids) : IConfiguredOwners
    {
        public bool IsConfiguredOwner(long telegramUserId) => ids.Contains(telegramUserId);
    }

    private sealed class Users : IPlatformUserRepository
    {
        private readonly List<PlatformUser> items = [];
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(items.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(items);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(items.Count);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) { items.Add(user); return Task.CompletedTask; }
    }
}
