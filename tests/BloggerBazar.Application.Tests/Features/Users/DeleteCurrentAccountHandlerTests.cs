using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.Team;
using BloggerBazar.Application.Features.Users;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloggerBazar.Application.Tests.Features.Users;

public sealed class DeleteCurrentAccountHandlerTests
{
    [Fact]
    public async Task Deletes_owned_profiles_and_records_a_correlated_audit_event()
    {
        var user = PlatformUser.Create(42, "User", "user");
        var blogger = BloggerProfile.Create(42, "Blogger", "tashkent", ["beauty"]);
        var brandFace = BrandFaceProfile.Create(42, "Brand", "tashkent", ["beauty"]);
        var business = BusinessProfile.Create(42, "Business", "tashkent");
        var audits = new InMemoryAuditLogs();
        var cache = new InMemoryCache();
        var handler = CreateHandler(user, blogger, brandFace, business, audits, cache);

        var result = await handler.Handle(new DeleteCurrentAccountCommand(42, "trace-42"), CancellationToken.None);

        Assert.False(result.AlreadyDeleted);
        Assert.True(user.IsDeleted);
        Assert.True(blogger.IsDeleted);
        Assert.True(brandFace.IsDeleted);
        Assert.True(business.IsDeleted);
        var audit = Assert.Single(audits.Entries);
        Assert.Equal("platform-user.account-deleted", audit.Action);
        Assert.Equal("trace-42", audit.CorrelationId);
        Assert.Equal(1, cache.Rotations);
        Assert.Equal(1, cache.CampaignRotations);
    }

    [Fact]
    public async Task Repeated_deletion_is_a_successful_no_op()
    {
        var user = PlatformUser.Create(42, "User", "user");
        var audits = new InMemoryAuditLogs();
        var cache = new InMemoryCache();
        var handler = CreateHandler(user, null, null, null, audits, cache);

        await handler.Handle(new DeleteCurrentAccountCommand(42, "trace-1"), CancellationToken.None);
        var repeated = await handler.Handle(new DeleteCurrentAccountCommand(42, "trace-2"), CancellationToken.None);

        Assert.True(repeated.AlreadyDeleted);
        Assert.Single(audits.Entries);
        Assert.Equal(1, cache.Rotations);
        Assert.Equal(1, cache.CampaignRotations);
    }

    [Fact]
    public async Task An_owner_with_a_team_removes_the_team_before_deleting_the_account()
    {
        var user = PlatformUser.Create(42, "User", "user");
        var business = BusinessProfile.Create(42, "Business", "tashkent");
        var audits = new InMemoryAuditLogs();
        var handler = new DeleteCurrentAccountHandler(new InMemoryUsers(user), new InMemoryBloggers(null), new InMemoryBrandFaces(null), new InMemoryBusinesses(business),
            new InMemoryFavorites(), new InMemoryBrandFaceFavorites(), audits, new UnitOfWork(), new InMemoryCache(), NullLogger<DeleteCurrentAccountHandler>.Instance, new TeamOf(3));

        var error = await Assert.ThrowsAsync<BusinessRuleConflictException>(() => handler.Handle(new DeleteCurrentAccountCommand(42, "trace-team"), CancellationToken.None));

        Assert.Equal(TeamCodes.TeamNotEmpty, error.Code);
        Assert.False(user.IsDeleted);
        Assert.False(business.IsDeleted);
        Assert.Empty(audits.Entries);
    }

    // Only the head count matters here.
    private sealed class TeamOf(int members) : IBusinessTeamRepository
    {
        public Task<int> CountActiveMembersAsync(Guid businessId, CancellationToken cancellationToken) => Task.FromResult(members);
        public Task<IReadOnlyList<TeamMemberRow>> GetMembersAsync(Guid businessId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TeamInvitationRow>> GetPendingInvitationsAsync(Guid businessId, DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessMember?> GetActiveMemberAsync(Guid businessId, Guid memberId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessMember?> GetActiveMembershipAsync(long telegramUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<long>> GetDecidersAsync(Guid businessId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddMemberAsync(BusinessMember member, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddInvitationAsync(BusinessInvitation invitation, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryRequestAsync(Guid invitationId, long telegramUserId, DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessInvitation?> GetInvitationByCodeHashAsync(string codeHash, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessInvitation?> GetInvitationAsync(Guid businessId, Guid invitationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CloseInvitationsAsync(Guid businessId, long closedByTelegramUserId, DateTime nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static DeleteCurrentAccountHandler CreateHandler(PlatformUser user, BloggerProfile? blogger, BrandFaceProfile? brandFace, BusinessProfile? business, InMemoryAuditLogs audits, InMemoryCache cache) =>
        new(new InMemoryUsers(user), new InMemoryBloggers(blogger), new InMemoryBrandFaces(brandFace), new InMemoryBusinesses(business), new InMemoryFavorites(), new InMemoryBrandFaceFavorites(), audits, new UnitOfWork(), cache, NullLogger<DeleteCurrentAccountHandler>.Instance);

    private sealed class InMemoryUsers(params PlatformUser[] users) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users.Where(user => !user.IsDeleted).Take(take).ToArray());
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Count(user => !user.IsDeleted));
        public Task<bool> SoftDeleteIfActiveAsync(long telegramUserId, long deletedByTelegramUserId, CancellationToken cancellationToken)
        {
            var user = users.SingleOrDefault(candidate => candidate.TelegramUserId == telegramUserId);
            if (user is null || user.IsDeleted) return Task.FromResult(false);
            user.SoftDelete(deletedByTelegramUserId);
            return Task.FromResult(true);
        }
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryBloggers(BloggerProfile? profile) : IBloggerProfileRepository
    {
        public Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profile?.Id == id && !profile.IsDeleted ? profile : null);
        public Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profile?.TelegramUserId == telegramUserId && !profile.IsDeleted ? profile : null);
        public Task<BloggerProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profile?.TelegramUserId == telegramUserId ? profile : null);
        public Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
        public Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryBrandFaces(BrandFaceProfile? profile) : IBrandFaceProfileRepository
    {
        public Task<BrandFaceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profile?.Id == id && !profile.IsDeleted ? profile : null);
        public Task<BrandFaceProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profile?.TelegramUserId == telegramUserId && !profile.IsDeleted ? profile : null);
        public Task<BrandFaceProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profile?.TelegramUserId == telegramUserId ? profile : null);
        public Task<IReadOnlyList<BrandFaceProfile>> GetAllAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>([]);
        public Task<IReadOnlyList<BrandFaceProfile>> SearchAsync(string? query, string? city, string? category, int skip, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BrandFaceProfile>>([]);
        public Task AddAsync(BrandFaceProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryBusinesses(BusinessProfile? profile) : IBusinessProfileRepository
    {
        public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(profile?.Id == id && !profile.IsDeleted ? profile : null);
        public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profile?.TelegramUserId == telegramUserId && !profile.IsDeleted ? profile : null);
        public Task<BusinessProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(profile?.TelegramUserId == telegramUserId ? profile : null);
        public Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryAuditLogs : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>(Entries.Take(take).ToArray());
    }

    private sealed class InMemoryFavorites : IFavoriteRepository
    {
        public Task<Favorite?> GetAsync(Guid platformUserId, Guid bloggerId, CancellationToken cancellationToken) => Task.FromResult<Favorite?>(null);
        public Task AddAsync(Favorite favorite, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> DeleteAsync(Guid platformUserId, Guid bloggerId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task DeleteForPlatformUserAsync(Guid platformUserId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryBrandFaceFavorites : IBrandFaceFavoriteRepository
    {
        public Task<BrandFaceFavorite?> GetAsync(Guid platformUserId, Guid brandFaceId, CancellationToken cancellationToken) => Task.FromResult<BrandFaceFavorite?>(null);
        public Task AddAsync(BrandFaceFavorite favorite, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> DeleteAsync(Guid platformUserId, Guid brandFaceId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task DeleteForPlatformUserAsync(Guid platformUserId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteForBrandFaceAsync(Guid brandFaceId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryCache : ICatalogCache
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

    private sealed class UnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}
