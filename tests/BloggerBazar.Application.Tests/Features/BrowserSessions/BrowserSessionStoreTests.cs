using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Application.Tests.Features.BrowserSessions;

// D64 stage B: sign-in codes and browser sessions in the cache.
public sealed class BrowserSessionStoreTests
{
    private static readonly BrowserDevice Laptop = new("chrome", "windows");

    [Fact]
    public async Task A_sign_in_is_collected_only_by_the_browser_that_asked_and_the_cache_keeps_only_hashes()
    {
        var cache = new RecordingCache();
        var store = CreateStore(cache, new ManualClock());

        var ticket = await store.StartSignInAsync(Laptop, CancellationToken.None);
        var other = await store.StartSignInAsync(Laptop, CancellationToken.None);

        Assert.Equal(22, ticket.Code.Length);
        Assert.Equal(43, ticket.Secret.Length);
        Assert.NotEqual(ticket.Code, other.Code);
        Assert.InRange(ticket.SignIn.Number, 10, 99);
        Assert.Equal(3, ticket.SignIn.Options.Distinct().Count());
        Assert.Contains(ticket.SignIn.Number, ticket.SignIn.Options);
        Assert.Equal(BrowserSignInStatuses.Pending, ticket.SignIn.Status);
        Assert.NotNull(await store.GetSignInForBrowserAsync(ticket.Code, ticket.Secret, CancellationToken.None));
        Assert.Null(await store.GetSignInForBrowserAsync(ticket.Code, other.Secret, CancellationToken.None));
        Assert.Null(await store.GetSignInForBrowserAsync(other.Code, ticket.Secret, CancellationToken.None));
        Assert.DoesNotContain(cache.Keys, key => key.Contains(ticket.Code, StringComparison.Ordinal) || key.Contains(ticket.Secret, StringComparison.Ordinal));
        Assert.Contains(DistributedBrowserSessionStore.SignInKeyFor(ticket.Code), cache.Keys);
    }

    [Fact]
    public async Task A_sign_in_is_decided_once_and_lapses_after_two_minutes()
    {
        var clock = new ManualClock();
        var store = CreateStore(new RecordingCache(), clock);
        var approved = await store.StartSignInAsync(Laptop, CancellationToken.None);
        var lapsing = await store.StartSignInAsync(Laptop, CancellationToken.None);

        Assert.True(await store.DecideSignInAsync(approved.Code, 42, CancellationToken.None));
        Assert.False(await store.DecideSignInAsync(approved.Code, null, CancellationToken.None));
        var decided = await store.GetSignInAsync(approved.Code, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(121));

        Assert.Equal(BrowserSignInStatuses.Approved, decided?.Status);
        Assert.Equal(42, decided?.ApprovedByTelegramUserId);
        Assert.Null(await store.GetSignInAsync(lapsing.Code, CancellationToken.None));
        Assert.False(await store.DecideSignInAsync(lapsing.Code, 42, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("0123456789abcdefghij+/")]
    public async Task Malformed_codes_and_tokens_are_unknown(string value)
    {
        var store = CreateStore(new RecordingCache(), new ManualClock());

        Assert.Null(await store.GetSignInAsync(value, CancellationToken.None));
        Assert.Null(await store.GetSessionAsync(value, CancellationToken.None));
    }

    [Fact]
    public async Task A_session_ends_after_two_idle_hours_and_use_keeps_it_alive()
    {
        var clock = new ManualClock();
        var store = CreateStore(new RecordingCache(), clock);
        var idle = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);
        var busy = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(100));
        Assert.NotNull(await store.GetSessionAsync(busy.Token, CancellationToken.None));
        clock.Advance(TimeSpan.FromMinutes(100));

        Assert.Null(await store.GetSessionAsync(idle.Token, CancellationToken.None));
        Assert.NotNull(await store.GetSessionAsync(busy.Token, CancellationToken.None));
    }

    [Fact]
    public async Task A_session_ends_twelve_hours_after_sign_in_however_active()
    {
        var clock = new ManualClock();
        var store = CreateStore(new RecordingCache(), clock);
        var ticket = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);

        for (var hour = 0; hour < 11; hour++)
        {
            clock.Advance(TimeSpan.FromHours(1));
            Assert.NotNull(await store.GetSessionAsync(ticket.Token, CancellationToken.None));
        }

        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(ticket.Session.CreatedAtUtc.AddHours(12), ticket.Session.ExpiresAtUtc);
        Assert.Null(await store.GetSessionAsync(ticket.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Looking_at_the_list_does_not_keep_a_browser_alive()
    {
        var clock = new ManualClock();
        var store = CreateStore(new RecordingCache(), clock);
        var ticket = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(90));
        Assert.Single(await store.ListSessionsAsync(42, CancellationToken.None));
        clock.Advance(TimeSpan.FromMinutes(40));

        Assert.Empty(await store.ListSessionsAsync(42, CancellationToken.None));
        Assert.Null(await store.GetSessionAsync(ticket.Token, CancellationToken.None));
    }

    [Fact]
    public async Task The_list_shows_a_persons_own_browsers_and_one_can_be_ended_by_its_id()
    {
        var clock = new ManualClock();
        var store = CreateStore(new RecordingCache(), clock);
        var laptop = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(5));
        var office = await store.CreateSessionAsync(42, new BrowserDevice("edge", "windows"), CancellationToken.None);
        var colleague = await store.CreateSessionAsync(43, Laptop, CancellationToken.None);

        var list = await store.ListSessionsAsync(42, CancellationToken.None);
        Assert.Equal([office.Session.Id, laptop.Session.Id], list.Select(session => session.Id));
        Assert.DoesNotContain(list, session => session.Id == colleague.Session.Id);

        Assert.False(await store.RemoveSessionByIdAsync(42, colleague.Session.Id, CancellationToken.None));
        Assert.True(await store.RemoveSessionByIdAsync(42, laptop.Session.Id, CancellationToken.None));
        Assert.Null(await store.GetSessionAsync(laptop.Token, CancellationToken.None));
        Assert.NotNull(await store.GetSessionAsync(office.Token, CancellationToken.None));
        Assert.NotNull(await store.GetSessionAsync(colleague.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Signing_out_everywhere_ends_every_current_browser_of_that_person_only()
    {
        var clock = new ManualClock();
        var cache = new RecordingCache();
        var store = CreateStore(cache, clock);
        var laptop = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);
        var unlisted = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);
        var colleague = await store.CreateSessionAsync(43, Laptop, CancellationToken.None);
        // A sign-in that lost the race for the list still ends: "everywhere" does not rely on the list.
        await cache.RemoveAsync("web:sessions:42");

        await store.RemoveAllSessionsAsync(42, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(1));
        var later = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);

        Assert.Null(await store.GetSessionAsync(laptop.Token, CancellationToken.None));
        Assert.Null(await store.GetSessionAsync(unlisted.Token, CancellationToken.None));
        Assert.NotNull(await store.GetSessionAsync(colleague.Token, CancellationToken.None));
        Assert.NotNull(await store.GetSessionAsync(later.Token, CancellationToken.None));
        Assert.Equal([later.Session.Id], (await store.ListSessionsAsync(42, CancellationToken.None)).Select(session => session.Id));
    }

    [Fact]
    public async Task Session_tokens_are_random_and_the_cache_keeps_only_their_hash()
    {
        var cache = new RecordingCache();
        var store = CreateStore(cache, new ManualClock());

        var first = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);
        var second = await store.CreateSessionAsync(42, Laptop, CancellationToken.None);

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Session.Id, second.Session.Id);
        Assert.Equal(43, first.Token.Length);
        Assert.DoesNotContain(cache.Keys, key => key.Contains(first.Token, StringComparison.Ordinal));
        Assert.Contains(DistributedBrowserSessionStore.SessionKeyFor(first.Token), cache.Keys);
        await store.RemoveSessionAsync(first.Token, CancellationToken.None);
        Assert.Null(await store.GetSessionAsync(first.Token, CancellationToken.None));
        Assert.Equal([second.Session.Id], (await store.ListSessionsAsync(42, CancellationToken.None)).Select(session => session.Id));
    }

    internal static DistributedBrowserSessionStore CreateStore(IDistributedCache cache, TimeProvider clock) =>
        new(cache, Options.Create(new BrowserSessionOptions()), clock);

    internal sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }

    // The memory cache keeps entries on its own wall clock; the store checks every time against its clock, so the
    // tests move time with ManualClock alone.
    internal sealed class RecordingCache : IDistributedCache
    {
        private readonly MemoryDistributedCache inner = new(Options.Create(new MemoryDistributedCacheOptions()));
        public List<string> Keys { get; } = [];
        public byte[]? Get(string key) => inner.Get(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { Keys.Add(key); inner.Set(key, value, Relax(options)); }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) { Keys.Add(key); return inner.SetAsync(key, value, Relax(options), token); }
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key) => inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);

        // Absolute ends computed from a moved clock may lie in the real past; keep such entries for the test instead.
        private static DistributedCacheEntryOptions Relax(DistributedCacheEntryOptions options) => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
            SlidingExpiration = options.SlidingExpiration
        };
    }
}
