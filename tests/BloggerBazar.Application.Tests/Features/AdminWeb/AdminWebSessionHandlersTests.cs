using System.Security.Authentication;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.AdminWeb;
using BloggerBazar.Application.Tests.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Tests.Features.AdminWeb;

public sealed class AdminWebSessionHandlersTests
{
    private static readonly Dictionary<string, string> AnyFields = new() { ["id"] = "42", ["hash"] = "h" };

    [Fact]
    public async Task An_admin_signs_in_and_the_journal_records_it()
    {
        var admin = User(42, PlatformRole.Admin);
        var sessions = new FakeSessions();
        var audit = new FakeAuditLogs();
        var unitOfWork = new SpyUnitOfWork();

        var result = await new StartAdminWebSessionHandler(new FakeValidator(42), sessions, new FakeAccess(42), new FakeUsers(admin), audit, unitOfWork)
            .Handle(new StartAdminWebSessionCommand(AnyFields, "corr-1"), CancellationToken.None);

        Assert.Equal(AdminWebRoles.Admin, result.Session.Role);
        Assert.Equal(42, result.Session.TelegramUserId);
        Assert.True(sessions.Has(result.Token));
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AdminWebAuditActions.SignedIn, entry.Action);
        Assert.Equal("corr-1", entry.CorrelationId);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task The_owner_is_reported_as_owner()
    {
        var result = await new StartAdminWebSessionHandler(new FakeValidator(7), new FakeSessions(), new FakeAccess(7), new FakeUsers(User(7, PlatformRole.Owner)), new FakeAuditLogs(), new SpyUnitOfWork())
            .Handle(new StartAdminWebSessionCommand(AnyFields), CancellationToken.None);

        Assert.Equal(AdminWebRoles.Owner, result.Session.Role);
    }

    [Fact]
    public async Task A_member_is_refused_without_a_session_and_the_attempt_is_recorded()
    {
        var sessions = new FakeSessions();
        var audit = new FakeAuditLogs();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new StartAdminWebSessionHandler(new FakeValidator(5), sessions, new FakeAccess(), new FakeUsers(User(5, PlatformRole.Member)), audit, new SpyUnitOfWork())
            .Handle(new StartAdminWebSessionCommand(AnyFields), CancellationToken.None));

        Assert.Equal(0, sessions.Count);
        Assert.Equal(AdminWebAuditActions.SignInDenied, Assert.Single(audit.Entries).Action);
    }

    [Fact]
    public async Task The_same_signed_data_cannot_sign_in_twice()
    {
        var sessions = new FakeSessions();
        var handler = new StartAdminWebSessionHandler(new FakeValidator(42), sessions, new FakeAccess(42), new FakeUsers(User(42, PlatformRole.Admin)), new FakeAuditLogs(), new SpyUnitOfWork());

        await handler.Handle(new StartAdminWebSessionCommand(AnyFields), CancellationToken.None);

        await Assert.ThrowsAsync<AuthenticationException>(() => handler.Handle(new StartAdminWebSessionCommand(AnyFields), CancellationToken.None));
        Assert.Equal(1, sessions.Count);
    }

    [Fact]
    public async Task A_session_of_someone_no_longer_allowed_is_dropped_on_the_next_request()
    {
        var sessions = new FakeSessions();
        var ticket = await sessions.CreateAsync(42, CancellationToken.None);
        var access = new FakeAccess(42);
        var handler = new GetAdminWebSessionHandler(sessions, access, new FakeUsers(User(42, PlatformRole.Admin)));

        Assert.Equal(42, (await handler.Handle(new GetAdminWebSessionQuery(ticket.Token), CancellationToken.None)).TelegramUserId);
        access.Revoke(42);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new GetAdminWebSessionQuery(ticket.Token), CancellationToken.None));
        Assert.False(sessions.Has(ticket.Token));
        await Assert.ThrowsAsync<AuthenticationException>(() => handler.Handle(new GetAdminWebSessionQuery(ticket.Token), CancellationToken.None));
    }

    [Fact]
    public async Task Logout_ends_the_session_and_is_recorded_once()
    {
        var sessions = new FakeSessions();
        var audit = new FakeAuditLogs();
        var ticket = await sessions.CreateAsync(42, CancellationToken.None);
        var handler = new EndAdminWebSessionHandler(sessions, audit, new SpyUnitOfWork());

        await handler.Handle(new EndAdminWebSessionCommand(ticket.Token), CancellationToken.None);
        await handler.Handle(new EndAdminWebSessionCommand(ticket.Token), CancellationToken.None);

        Assert.False(sessions.Has(ticket.Token));
        Assert.Equal(AdminWebAuditActions.SignedOut, Assert.Single(audit.Entries).Action);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("first-name")]
    [InlineData("")]
    public void Field_names_outside_the_widget_format_are_invalid(string name)
    {
        var result = new StartAdminWebSessionValidator().Validate(new StartAdminWebSessionCommand(new Dictionary<string, string> { [name] = "1", ["hash"] = "h" }));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Too_many_or_too_long_fields_are_invalid()
    {
        var many = Enumerable.Range(0, 17).ToDictionary(index => $"f{(char)('a' + index)}", _ => "1");
        var tooLong = new Dictionary<string, string> { ["hash"] = new string('a', 513) };
        var validator = new StartAdminWebSessionValidator();

        Assert.False(validator.Validate(new StartAdminWebSessionCommand(many)).IsValid);
        Assert.False(validator.Validate(new StartAdminWebSessionCommand(tooLong)).IsValid);
        Assert.True(validator.Validate(new StartAdminWebSessionCommand(new Dictionary<string, string> { ["auth_date"] = "1", ["hash"] = "h" })).IsValid);
    }

    private static PlatformUser User(long telegramUserId, PlatformRole role)
    {
        var user = PlatformUser.Create(telegramUserId, "Umid", "umid");
        user.SetRole(role);
        return user;
    }

    private sealed class FakeValidator(long telegramUserId) : ITelegramLoginWidgetValidator
    {
        public TelegramLoginWidgetUser Validate(IReadOnlyDictionary<string, string> fields) => new(telegramUserId, "Umid", "umid", "signed-hash");
    }

    private sealed class FakeAccess(params long[] allowed) : IAdminAccessPolicy
    {
        private readonly HashSet<long> ids = [.. allowed];
        public void Revoke(long telegramUserId) => ids.Remove(telegramUserId);
        public void EnsureAllowed(long telegramUserId)
        {
            if (!ids.Contains(telegramUserId)) throw new UnauthorizedAccessException("Administrator access is required.");
        }
    }

    private sealed class FakeSessions : IAdminWebSessionStore
    {
        private readonly Dictionary<string, AdminWebSession> items = [];
        private readonly HashSet<string> usedSignIns = [];
        public int Count => items.Count;
        public bool Has(string token) => items.ContainsKey(token);
        public Task<bool> TryUseSignInAsync(string hash, CancellationToken cancellationToken) => Task.FromResult(usedSignIns.Add(hash));
        public Task<AdminWebSessionTicket> CreateAsync(long telegramUserId, CancellationToken cancellationToken)
        {
            var token = Guid.NewGuid().ToString("N");
            var session = new AdminWebSession(telegramUserId, DateTime.UtcNow, DateTime.UtcNow.AddHours(8));
            items[token] = session;
            return Task.FromResult(new AdminWebSessionTicket(token, session));
        }
        public Task<AdminWebSession?> GetAsync(string token, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(token));
        public Task RemoveAsync(string token, CancellationToken cancellationToken) { items.Remove(token); return Task.CompletedTask; }
    }

    private sealed class FakeUsers(params PlatformUser[] users) : IPlatformUserRepository
    {
        public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => Task.FromResult(users.SingleOrDefault(user => user.TelegramUserId == telegramUserId));
        public Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>(users);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken) => Task.FromResult(users.Length);
        public Task AddAsync(PlatformUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeAuditLogs : IAuditLogRepository
    {
        public List<AuditLog> Entries { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AuditLog>>(Entries);
    }
}
