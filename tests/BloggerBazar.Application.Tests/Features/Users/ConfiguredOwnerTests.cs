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

    // Audit 2026-10-22: a former co-owner (id taken out of the settings) becomes a member on the next sign-in, journaled.
    [Fact]
    public async Task An_owner_no_longer_in_the_settings_becomes_a_member_with_a_journal_entry()
    {
        var users = new Users();
        var former = PlatformUser.Create(9, "Former", null);
        former.SetRole(PlatformRole.Owner);
        await users.AddAsync(former, CancellationToken.None);
        var audit = new AuditLogs();

        var result = await new GetCurrentPlatformUserHandler(users, new SpyUnitOfWork(), new Owners(42), audit)
            .Handle(new GetCurrentPlatformUserCommand(9, "Former", null), CancellationToken.None);

        Assert.Equal(PlatformRole.Member, result.Role);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal("platform-user.owner-revoked", entry.Action);
        Assert.Equal("9", entry.TargetId);
    }

    private sealed class AuditLogs : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>(Entries);
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
