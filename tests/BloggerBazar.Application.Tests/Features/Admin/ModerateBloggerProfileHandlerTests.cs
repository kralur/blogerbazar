using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.Admin;

public sealed class ModerateBloggerProfileHandlerTests
{
    [Fact]
    public async Task Approves_pending_profile_for_administrator()
    {
        var profile = BloggerProfile.Create(101, "Madina", "Tashkent", ["Lifestyle"]);
        var handler = new ModerateBloggerProfileHandler(new InMemoryBloggerRepository(profile), new AllowAdminAccessPolicy(), new SpyUnitOfWork(), new SpyAuditLogs());

        var result = await handler.Handle(new ModerateBloggerProfileCommand(profile.Id, 1, true), CancellationToken.None);

        Assert.Equal((int)BloggerStatus.Approved, result.Status);
        Assert.True(profile.IsVerified);
    }

    [Theory]
    [InlineData(true, false, "blogger-profile.approved")]
    [InlineData(false, false, "blogger-profile.rejected")]
    [InlineData(false, true, "blogger-profile.needs-changes")]
    public async Task Writes_every_decision_to_the_journal(bool approve, bool needsChanges, string action)
    {
        var profile = BloggerProfile.Create(101, "Madina", "Tashkent", ["Lifestyle"]);
        var auditLogs = new SpyAuditLogs();
        var handler = new ModerateBloggerProfileHandler(new InMemoryBloggerRepository(profile), new AllowAdminAccessPolicy(), new SpyUnitOfWork(), auditLogs);

        await handler.Handle(new ModerateBloggerProfileCommand(profile.Id, 7, approve, needsChanges), CancellationToken.None);

        var entry = Assert.Single(auditLogs.Entries);
        Assert.Equal(action, entry.Action);
        Assert.Equal(7, entry.ActorTelegramUserId);
        Assert.Equal("BloggerProfile", entry.TargetType);
        Assert.Equal(profile.Id.ToString(), entry.TargetId);
    }

    [Fact]
    public async Task Writes_nothing_when_the_profile_is_no_longer_pending()
    {
        var profile = BloggerProfile.Create(101, "Madina", "Tashkent", ["Lifestyle"]);
        profile.Approve();
        var auditLogs = new SpyAuditLogs();
        var handler = new ModerateBloggerProfileHandler(new InMemoryBloggerRepository(profile), new AllowAdminAccessPolicy(), new SpyUnitOfWork(), auditLogs);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ModerateBloggerProfileCommand(profile.Id, 7, false), CancellationToken.None));
        Assert.Empty(auditLogs.Entries);
    }

    [Fact]
    public async Task Rejects_moderation_from_non_administrator()
    {
        var profile = BloggerProfile.Create(101, "Madina", "Tashkent", ["Lifestyle"]);
        var handler = new ModerateBloggerProfileHandler(new InMemoryBloggerRepository(profile), new DenyAdminAccessPolicy(), new SpyUnitOfWork(), new SpyAuditLogs());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ModerateBloggerProfileCommand(profile.Id, 2, true), CancellationToken.None));
    }

    private sealed class InMemoryBloggerRepository(BloggerProfile profile) : IBloggerProfileRepository
    {
        public Task AddAsync(BloggerProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(id == profile.Id ? profile : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult<BloggerProfile?>(null);
        public Task<IReadOnlyList<BloggerProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([profile]);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    }

    private sealed class AllowAdminAccessPolicy : IAdminAccessPolicy
    {
        public void EnsureAllowed(long telegramUserId) { }
    }

    private sealed class DenyAdminAccessPolicy : IAdminAccessPolicy
    {
        public void EnsureAllowed(long telegramUserId) => throw new UnauthorizedAccessException();
    }

    private sealed class SpyAuditLogs : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>(Entries);
    }

    private sealed class SpyUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
