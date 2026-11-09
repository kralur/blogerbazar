using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Security;

// D64 stage B: browser sign-in requests and browser sessions in the distributed cache (Redis in production, memory
// when Redis is not configured). Keys are SHA-256 hashes of the code and of the token, so a copy of the cache gives
// neither working links nor working cookies. The idle limit is kept in the session itself rather than by the cache's
// sliding expiration, so that listing a person's browsers does not keep them alive. The cache cannot list keys, so
// each person has a small index of their sessions for the list; "sign out everywhere" does not rely on that index:
// it stores the moment, and sessions created up to it no longer count.
internal sealed class DistributedBrowserSessionStore(IDistributedCache cache, IOptions<BrowserSessionOptions> options, TimeProvider? clock = null) : IBrowserSessionStore
{
    private const string SignInKeyPrefix = "web:sign-in:";
    private const string SessionKeyPrefix = "web:session:";
    private const string IndexKeyPrefix = "web:sessions:";
    private const string EndedKeyPrefix = "web:ended:";
    private const int CodeBytes = 16;
    private const int SecretBytes = 32;
    private const int TokenBytes = 32;
    private const int SessionIdBytes = 12;
    // The last-used time is written at most this often, not on every request.
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
    private static readonly int CodeLength = Base64Url.GetEncodedLength(CodeBytes);
    private static readonly int SecretLength = Base64Url.GetEncodedLength(SecretBytes);
    private static readonly int TokenLength = Base64Url.GetEncodedLength(TokenBytes);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    private BrowserSessionOptions Settings => options.Value;
    private TimeSpan SessionLifetime => TimeSpan.FromHours(Math.Max(1, Settings.SessionHours));
    private TimeSpan IdleLimit => TimeSpan.FromMinutes(Math.Max(1, Settings.IdleMinutes));

    public async Task<BrowserSignInTicket> StartSignInAsync(BrowserDevice device, CancellationToken cancellationToken)
    {
        var code = NewToken(CodeBytes);
        var secret = NewToken(SecretBytes);
        var now = Now();
        var number = RandomNumberGenerator.GetInt32(10, 100);
        var signIn = new BrowserSignIn(device, number, OptionsAround(number), BrowserSignInStatuses.Pending, null, now,
            now.AddSeconds(Math.Clamp(Settings.SignInSeconds, 30, 600)));
        await SaveSignInAsync(code, new StoredSignIn(Hash(secret), signIn), cancellationToken);
        return new BrowserSignInTicket(code, secret, signIn);
    }

    public async Task<BrowserSignIn?> GetSignInAsync(string code, CancellationToken cancellationToken) =>
        (await ReadSignInAsync(code, cancellationToken))?.SignIn;

    public async Task<BrowserSignIn?> GetSignInForBrowserAsync(string code, string secret, CancellationToken cancellationToken)
    {
        if (!IsToken(secret, SecretLength) || await ReadSignInAsync(code, cancellationToken) is not { } stored)
        {
            return null;
        }

        var expected = Convert.FromHexString(stored.SecretHash);
        var supplied = SHA256.HashData(Encoding.ASCII.GetBytes(secret));
        return CryptographicOperations.FixedTimeEquals(expected, supplied) ? stored.SignIn : null;
    }

    public async Task<bool> DecideSignInAsync(string code, long? approvedByTelegramUserId, CancellationToken cancellationToken)
    {
        // Read-then-write, not atomic: two decisions in the same instant could both pass, but both would have to come
        // from the Mini App with this code, and the browser collects the session only once (RemoveSignInAsync first).
        if (await ReadSignInAsync(code, cancellationToken) is not { } stored || stored.SignIn.Status != BrowserSignInStatuses.Pending)
        {
            return false;
        }

        var decided = stored.SignIn with
        {
            Status = approvedByTelegramUserId is null ? BrowserSignInStatuses.Declined : BrowserSignInStatuses.Approved,
            ApprovedByTelegramUserId = approvedByTelegramUserId
        };
        await SaveSignInAsync(code, stored with { SignIn = decided }, cancellationToken);
        return true;
    }

    public Task RemoveSignInAsync(string code, CancellationToken cancellationToken) =>
        IsToken(code, CodeLength) ? cache.RemoveAsync(SignInKeyFor(code), cancellationToken) : Task.CompletedTask;

    public async Task<BrowserSessionTicket> CreateSessionAsync(long telegramUserId, BrowserDevice device, CancellationToken cancellationToken)
    {
        var token = NewToken(TokenBytes);
        var now = Now();
        var session = new BrowserSession(NewToken(SessionIdBytes), telegramUserId, device, now, now, now.Add(SessionLifetime));
        await WriteSessionAsync(SessionKeyFor(token), session, cancellationToken);
        var index = await ReadIndexAsync(telegramUserId, cancellationToken);
        await WriteIndexAsync(telegramUserId, [.. index, new IndexEntry(session.Id, SessionKeyFor(token))], cancellationToken);
        return new BrowserSessionTicket(token, session);
    }

    public async Task<BrowserSession?> GetSessionAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsToken(token, TokenLength))
        {
            return null;
        }

        var key = SessionKeyFor(token);
        var session = await ReadLiveSessionAsync(key, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var now = Now();
        if (now - session.LastSeenAtUtc >= TouchInterval)
        {
            session = session with { LastSeenAtUtc = now };
            await WriteSessionAsync(key, session, cancellationToken);
        }

        return session;
    }

    public async Task<IReadOnlyList<BrowserSession>> ListSessionsAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var index = await ReadIndexAsync(telegramUserId, cancellationToken);
        var live = new List<(IndexEntry Entry, BrowserSession Session)>();
        foreach (var entry in index)
        {
            if (await ReadLiveSessionAsync(entry.Key, cancellationToken) is { } session && session.TelegramUserId == telegramUserId)
            {
                live.Add((entry, session));
            }
        }

        if (live.Count != index.Count)
        {
            await WriteIndexAsync(telegramUserId, [.. live.Select(item => item.Entry)], cancellationToken);
        }

        return [.. live.Select(item => item.Session).OrderByDescending(session => session.CreatedAtUtc)];
    }

    public async Task RemoveSessionAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsToken(token, TokenLength))
        {
            return;
        }

        var key = SessionKeyFor(token);
        var session = await ReadSessionAsync(key, cancellationToken);
        await cache.RemoveAsync(key, cancellationToken);
        if (session is not null)
        {
            var index = await ReadIndexAsync(session.TelegramUserId, cancellationToken);
            await WriteIndexAsync(session.TelegramUserId, [.. index.Where(entry => entry.Key != key)], cancellationToken);
        }
    }

    public async Task<bool> RemoveSessionByIdAsync(long telegramUserId, string sessionId, CancellationToken cancellationToken)
    {
        var index = await ReadIndexAsync(telegramUserId, cancellationToken);
        var entry = index.FirstOrDefault(candidate => string.Equals(candidate.Id, sessionId, StringComparison.Ordinal));
        if (entry is null)
        {
            return false;
        }

        var live = await ReadLiveSessionAsync(entry.Key, cancellationToken);
        await cache.RemoveAsync(entry.Key, cancellationToken);
        await WriteIndexAsync(telegramUserId, [.. index.Where(candidate => candidate != entry)], cancellationToken);
        return live is not null && live.TelegramUserId == telegramUserId;
    }

    public async Task RemoveAllSessionsAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        // Kept as long as a session can live, so every session that existed at this moment is covered, listed or not.
        await cache.SetStringAsync(EndedKeyFor(telegramUserId), Now().ToString("O", CultureInfo.InvariantCulture), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = SessionLifetime
        }, cancellationToken);
        foreach (var entry in await ReadIndexAsync(telegramUserId, cancellationToken))
        {
            await cache.RemoveAsync(entry.Key, cancellationToken);
        }

        await cache.RemoveAsync(IndexKeyFor(telegramUserId), cancellationToken);
    }

    internal static string SessionKeyFor(string token) => SessionKeyPrefix + HashText(token);

    internal static string SignInKeyFor(string code) => SignInKeyPrefix + HashText(code);

    private async Task<StoredSignIn?> ReadSignInAsync(string code, CancellationToken cancellationToken)
    {
        if (!IsToken(code, CodeLength))
        {
            return null;
        }

        var payload = await cache.GetStringAsync(SignInKeyFor(code), cancellationToken);
        var stored = payload is null ? null : JsonSerializer.Deserialize<StoredSignIn>(payload, SerializerOptions);
        return stored is null || stored.SignIn.ExpiresAtUtc <= Now() ? null : stored;
    }

    private Task SaveSignInAsync(string code, StoredSignIn stored, CancellationToken cancellationToken) =>
        cache.SetStringAsync(SignInKeyFor(code), JsonSerializer.Serialize(stored, SerializerOptions), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = new DateTimeOffset(stored.SignIn.ExpiresAtUtc, TimeSpan.Zero)
        }, cancellationToken);

    private async Task<BrowserSession?> ReadSessionAsync(string key, CancellationToken cancellationToken)
    {
        var payload = await cache.GetStringAsync(key, cancellationToken);
        return payload is null ? null : JsonSerializer.Deserialize<BrowserSession>(payload, SerializerOptions);
    }

    // The session when it is still within its fixed end, its idle limit and any "sign out everywhere"; otherwise the
    // entry is dropped and null returned.
    private async Task<BrowserSession?> ReadLiveSessionAsync(string key, CancellationToken cancellationToken)
    {
        var session = await ReadSessionAsync(key, cancellationToken);
        if (session is null)
        {
            return null;
        }

        var now = Now();
        if (session.ExpiresAtUtc <= now || now - session.LastSeenAtUtc >= IdleLimit || await EndedEverywhereAsync(session, cancellationToken))
        {
            await cache.RemoveAsync(key, cancellationToken);
            return null;
        }

        return session;
    }

    private Task WriteSessionAsync(string key, BrowserSession session, CancellationToken cancellationToken) =>
        cache.SetStringAsync(key, JsonSerializer.Serialize(session, SerializerOptions), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = new DateTimeOffset(session.ExpiresAtUtc, TimeSpan.Zero)
        }, cancellationToken);

    private async Task<IReadOnlyList<IndexEntry>> ReadIndexAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var payload = await cache.GetStringAsync(IndexKeyFor(telegramUserId), cancellationToken);
        return payload is null ? [] : JsonSerializer.Deserialize<IndexEntry[]>(payload, SerializerOptions) ?? [];
    }

    // Read-then-write like the rest: two sign-ins of one person in the same instant may leave one of them off the
    // list. That session still works and still ends with "sign out everywhere", which does not use the list.
    private Task WriteIndexAsync(long telegramUserId, IReadOnlyList<IndexEntry> entries, CancellationToken cancellationToken) =>
        entries.Count == 0
            ? cache.RemoveAsync(IndexKeyFor(telegramUserId), cancellationToken)
            : cache.SetStringAsync(IndexKeyFor(telegramUserId), JsonSerializer.Serialize(entries, SerializerOptions), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SessionLifetime
            }, cancellationToken);

    private async Task<bool> EndedEverywhereAsync(BrowserSession session, CancellationToken cancellationToken)
    {
        var ended = await cache.GetStringAsync(EndedKeyFor(session.TelegramUserId), cancellationToken);
        return ended is not null
            && DateTime.TryParse(ended, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endedAtUtc)
            && session.CreatedAtUtc <= endedAtUtc;
    }

    // The right number and two others, in random order.
    private static int[] OptionsAround(int number)
    {
        var options = new List<int> { number };
        while (options.Count < 3)
        {
            var candidate = RandomNumberGenerator.GetInt32(10, 100);
            if (!options.Contains(candidate))
            {
                options.Add(candidate);
            }
        }

        var shuffled = options.ToArray();
        RandomNumberGenerator.Shuffle<int>(shuffled);
        return shuffled;
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;

    private static string NewToken(int bytes) => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(bytes));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value)));

    private static string HashText(string value) => Hash(value).ToLowerInvariant();

    private static string IndexKeyFor(long telegramUserId) => IndexKeyPrefix + telegramUserId.ToString(CultureInfo.InvariantCulture);

    private static string EndedKeyFor(long telegramUserId) => EndedKeyPrefix + telegramUserId.ToString(CultureInfo.InvariantCulture);

    internal static bool IsToken(string? value, int length) =>
        value is not null && value.Length == length && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    internal static bool IsCode(string? value) => IsToken(value, CodeLength);

    private sealed record StoredSignIn(string SecretHash, BrowserSignIn SignIn);

    // Key: the cache key of the session (a hash of its token), never the token.
    private sealed record IndexEntry(string Id, string Key);
}
