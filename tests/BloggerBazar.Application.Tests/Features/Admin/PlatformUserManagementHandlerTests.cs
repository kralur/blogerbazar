using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Admin;

public sealed class PlatformUserManagementHandlerTests
{
    [Fact]
    public async Task Owner_can_assign_role_and_audit_entry_is_persisted()
    {
        var owner = PlatformUser.Create(1, "Owner", "owner");
        owner.SetRole(PlatformRole.Owner);
        var member = PlatformUser.Create(2, "Member", "member");
        var users = new InMemoryPlatformUserRepository(owner, member);
        var auditLogs = new InMemoryAuditLogRepository();
        var handler = new UpdatePlatformUserRoleHandler(users, auditLogs, new SpyUnitOfWork());

        var result = await handler.Handle(new UpdatePlatformUserRoleCommand(owner.TelegramUserId, member.TelegramUserId, PlatformRole.Moderator), CancellationToken.None);

        Assert.Equal(PlatformRole.Moderator, result.Role);
        var entry = Assert.Single(auditLogs.Entries);
        Assert.Equal("platform-user.role-updated", entry.Action);
        Assert.Equal(member.TelegramUserId.ToString(), entry.TargetId);
    }

    [Fact]
    public async Task Non_owner_cannot_assign_a_role()
    {
        var member = PlatformUser.Create(2, "Member", "member");
        var users = new InMemoryPlatformUserRepository(member);
        var handler = new UpdatePlatformUserRoleHandler(users, new InMemoryAuditLogRepository(), new SpyUnitOfWork());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new UpdatePlatformUserRoleCommand(member.TelegramUserId, 99, PlatformRole.Admin), CancellationToken.None));
    }

    [Fact]
    public async Task Owner_can_block_registered_user_and_audit_entry_is_persisted()
    {
        var owner = PlatformUser.Create(1, "Owner", "owner");
        owner.SetRole(PlatformRole.Owner);
        var member = PlatformUser.Create(2, "Member", "member");
        var auditLogs = new InMemoryAuditLogRepository();
        var handler = new SetPlatformUserBlockedHandler(new InMemoryPlatformUserRepository(owner, member), auditLogs, new SpyUnitOfWork());

        var result = await handler.Handle(new SetPlatformUserBlockedCommand(owner.TelegramUserId, member.TelegramUserId, true), CancellationToken.None);

        Assert.True(result.IsBlocked);
        Assert.Equal("platform-user.blocked", Assert.Single(auditLogs.Entries).Action);
    }

    [Fact]
    public async Task Blocking_or_unblocking_a_user_rotates_the_catalog_namespace()
    {
        var owner = PlatformUser.Create(1, "Owner", "owner");
        owner.SetRole(PlatformRole.Owner);
        var member = PlatformUser.Create(2, "Member", "member");
        var cache = new RecordingCache();
        var handler = new SetPlatformUserBlockedHandler(new InMemoryPlatformUserRepository(owner, member), new InMemoryAuditLogRepository(), new SpyUnitOfWork(), cache);

        await handler.Handle(new SetPlatformUserBlockedCommand(owner.TelegramUserId, member.TelegramUserId, true), CancellationToken.None);
        await handler.Handle(new SetPlatformUserBlockedCommand(owner.TelegramUserId, member.TelegramUserId, false), CancellationToken.None);

        Assert.Equal(2, cache.Rotations);
        Assert.Equal(2, cache.CampaignRotations);
    }

    private sealed class InMemoryPlatformUserRepository(params PlatformUser[] initial) : IPlatformUserRepository
    {
        private readonly List<PlatformUser> users = [.. initial];
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users.Where(user => !user.IsDeleted).Take(take).ToArray());
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Count(user => !user.IsDeleted));
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) { users.Add(user); return Task.CompletedTask; }
    }

    private sealed class InMemoryAuditLogRepository : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>(Entries.Take(take).ToArray());
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class RecordingCache : ICatalogCache
    {
        public int Rotations { get; private set; }
        public int CampaignRotations { get; private set; }
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class => Task.FromResult<T?>(null);
        public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class => Task.CompletedTask;
        public Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) { Rotations++; return Task.CompletedTask; }
        public Task RotateNamespaceVersionAsync(string catalog, CancellationToken cancellationToken)
        {
            if (catalog == "campaigns") CampaignRotations++;
            return Task.CompletedTask;
        }
    }
}
