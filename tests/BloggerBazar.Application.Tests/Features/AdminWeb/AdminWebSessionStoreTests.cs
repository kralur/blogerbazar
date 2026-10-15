using System.Text.Json;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Application.Tests.Features.AdminWeb;

public sealed class AdminWebSessionStoreTests
{
    [Fact]
    public async Task A_new_session_lives_until_it_is_removed()
    {
        var store = CreateStore(new RecordingCache());

        var ticket = await store.CreateAsync(42, CancellationToken.None);
        var found = await store.GetAsync(ticket.Token, CancellationToken.None);
        await store.RemoveAsync(ticket.Token, CancellationToken.None);

        Assert.Equal(42, found?.TelegramUserId);
        Assert.Equal(ticket.Session.CreatedAtUtc.AddHours(8), ticket.Session.ExpiresAtUtc);
        Assert.Null(await store.GetAsync(ticket.Token, CancellationToken.None));
    }

    [Fact]
    public async Task Tokens_are_random_and_the_cache_keeps_only_their_hash()
    {
        var cache = new RecordingCache();
        var store = CreateStore(cache);

        var first = await store.CreateAsync(42, CancellationToken.None);
        var second = await store.CreateAsync(42, CancellationToken.None);

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(43, first.Token.Length);
        Assert.DoesNotContain(cache.Keys, key => key.Contains(first.Token, StringComparison.Ordinal));
        Assert.Contains(DistributedAdminWebSessionStore.KeyFor(first.Token), cache.Keys);
    }

    [Fact]
    public async Task A_session_past_its_fixed_end_is_gone_even_if_the_cache_still_has_it()
    {
        var cache = new RecordingCache();
        var store = CreateStore(cache);
        var ticket = await store.CreateAsync(42, CancellationToken.None);
        var stale = new AdminWebSession(42, DateTime.UtcNow.AddHours(-9), DateTime.UtcNow.AddMinutes(-1));
        await cache.SetStringAsync(DistributedAdminWebSessionStore.KeyFor(ticket.Token), JsonSerializer.Serialize(stale, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Null(await store.GetAsync(ticket.Token, CancellationToken.None));
        Assert.Null(await cache.GetStringAsync(DistributedAdminWebSessionStore.KeyFor(ticket.Token)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("0123456789012345678901234567890123456789+/=")]
    public async Task Malformed_tokens_are_unknown(string token)
    {
        Assert.Null(await CreateStore(new RecordingCache()).GetAsync(token, CancellationToken.None));
    }

    [Fact]
    public async Task A_signed_sign_in_can_be_used_once()
    {
        var store = CreateStore(new RecordingCache());

        Assert.True(await store.TryUseSignInAsync("abc", CancellationToken.None));
        Assert.False(await store.TryUseSignInAsync("abc", CancellationToken.None));
        Assert.True(await store.TryUseSignInAsync("def", CancellationToken.None));
    }

    private static DistributedAdminWebSessionStore CreateStore(IDistributedCache cache) => new(
        cache,
        Options.Create(new AdminWebOptions { Origin = "https://admin.example", SessionHours = 8, IdleMinutes = 60, MaxAuthAgeSeconds = 300 }),
        Options.Create(new TelegramOptions { MaxInitDataClockSkewSeconds = 60 }));

    private sealed class RecordingCache : IDistributedCache
    {
        private readonly MemoryDistributedCache inner = new(Options.Create(new MemoryDistributedCacheOptions()));
        public List<string> Keys { get; } = [];
        public byte[]? Get(string key) => inner.Get(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { Keys.Add(key); inner.Set(key, value, options); }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) { Keys.Add(key); return inner.SetAsync(key, value, options, token); }
        public void Refresh(string key) => inner.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
        public void Remove(string key) => inner.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
    }
}
