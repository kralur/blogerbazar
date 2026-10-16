using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Security;

// Web admin sessions in the distributed cache (Redis in production, memory when Redis is not configured).
// The key is a SHA-256 of the token, so a copy of the cache does not give working cookies. The cache ends an idle
// session (sliding expiration) and the stored fixed end is checked on every read as well. The cache cannot list a
// person's sessions, so "sign out everywhere" stores the moment instead: sessions created up to it no longer count.
internal sealed class DistributedAdminWebSessionStore(IDistributedCache cache, IOptions<AdminWebOptions> options, IOptions<TelegramOptions> telegramOptions) : IAdminWebSessionStore
{
    private const string SessionKeyPrefix = "admin-web:session:";
    private const string SignInKeyPrefix = "admin-web:sign-in:";
    private const string EndedKeyPrefix = "admin-web:ended:";
    private const int TokenBytes = 32;
    private static readonly int TokenLength = Base64Url.GetEncodedLength(TokenBytes);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> TryUseSignInAsync(string hash, CancellationToken cancellationToken)
    {
        // Read-then-write, not atomic: two requests with the same data in the same instant could both pass. Both would
        // still need the signed data of a real admin sign-in, which is valid for a few minutes only.
        var key = SignInKeyPrefix + hash;
        if (await cache.GetStringAsync(key, cancellationToken) is not null)
        {
            return false;
        }

        var lifetime = TimeSpan.FromSeconds(Math.Max(1, options.Value.MaxAuthAgeSeconds + telegramOptions.Value.MaxInitDataClockSkewSeconds));
        await cache.SetStringAsync(key, "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime }, cancellationToken);
        return true;
    }

    public async Task<AdminWebSessionTicket> CreateAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var now = DateTime.UtcNow;
        var session = new AdminWebSession(telegramUserId, now, now.AddHours(Math.Max(1, options.Value.SessionHours)));
        await cache.SetStringAsync(KeyFor(token), JsonSerializer.Serialize(session, SerializerOptions), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = new DateTimeOffset(session.ExpiresAtUtc, TimeSpan.Zero),
            SlidingExpiration = TimeSpan.FromMinutes(Math.Max(1, options.Value.IdleMinutes))
        }, cancellationToken);
        return new AdminWebSessionTicket(token, session);
    }

    public async Task<AdminWebSession?> GetAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsWellFormed(token))
        {
            return null;
        }

        // Reading also moves the idle expiry forward (sliding expiration).
        var payload = await cache.GetStringAsync(KeyFor(token), cancellationToken);
        if (payload is null)
        {
            return null;
        }

        var session = JsonSerializer.Deserialize<AdminWebSession>(payload, SerializerOptions);
        if (session is null || session.ExpiresAtUtc <= DateTime.UtcNow || await EndedEverywhereAsync(session, cancellationToken))
        {
            await cache.RemoveAsync(KeyFor(token), cancellationToken);
            return null;
        }

        return session;
    }

    public Task RemoveAsync(string token, CancellationToken cancellationToken) =>
        IsWellFormed(token) ? cache.RemoveAsync(KeyFor(token), cancellationToken) : Task.CompletedTask;

    public async Task RemoveAllAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        // Kept as long as a session can live, so every session that existed at this moment is covered.
        var ended = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await cache.SetStringAsync(EndedKeyPrefix + telegramUserId.ToString(CultureInfo.InvariantCulture), ended, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Math.Max(1, options.Value.SessionHours))
        }, cancellationToken);
    }

    private async Task<bool> EndedEverywhereAsync(AdminWebSession session, CancellationToken cancellationToken)
    {
        var ended = await cache.GetStringAsync(EndedKeyPrefix + session.TelegramUserId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return ended is not null
            && DateTime.TryParse(ended, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endedAtUtc)
            && session.CreatedAtUtc <= endedAtUtc;
    }

    internal static string KeyFor(string token) =>
        SessionKeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant();

    private static bool IsWellFormed(string? token) =>
        token is not null && token.Length == TokenLength && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
