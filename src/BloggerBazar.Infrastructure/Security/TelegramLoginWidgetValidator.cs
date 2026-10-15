using System.Globalization;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Security;

// https://core.telegram.org/widgets/login: data_check_string is every received field except hash, sorted by name,
// "name=value" joined by line feeds; hash = hex(HMAC-SHA256(data_check_string, SHA256(bot token))).
// Unknown fields are part of the check too, so a field Telegram adds later does not break the signature.
internal sealed class TelegramLoginWidgetValidator(IOptions<TelegramOptions> telegramOptions, IOptions<AdminWebOptions> adminWebOptions) : ITelegramLoginWidgetValidator
{
    private const int HashHexLength = 64;

    public TelegramLoginWidgetUser Validate(IReadOnlyDictionary<string, string> fields)
    {
        var botToken = telegramOptions.Value.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException("Telegram:BotToken is not configured.");
        }

        if (!fields.TryGetValue("hash", out var suppliedHash) || !TryParseHash(suppliedHash, out var supplied))
        {
            throw new AuthenticationException("Telegram login data is missing a valid hash.");
        }

        var dataCheckString = string.Join('\n', fields
            .Where(pair => !string.Equals(pair.Key, "hash", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var calculated = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        if (!CryptographicOperations.FixedTimeEquals(calculated, supplied))
        {
            throw new AuthenticationException("Telegram login signature is invalid.");
        }

        if (!fields.TryGetValue("auth_date", out var authDate)
            || !long.TryParse(authDate, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds)
            || unixSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            throw new AuthenticationException("Telegram login data has expired.");
        }

        var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var now = DateTimeOffset.UtcNow;
        if (authenticatedAt > now.AddSeconds(telegramOptions.Value.MaxInitDataClockSkewSeconds)
            || now - authenticatedAt > TimeSpan.FromSeconds(adminWebOptions.Value.MaxAuthAgeSeconds))
        {
            throw new AuthenticationException("Telegram login data has expired.");
        }

        if (!fields.TryGetValue("id", out var idValue)
            || !long.TryParse(idValue, NumberStyles.None, CultureInfo.InvariantCulture, out var telegramUserId)
            || telegramUserId <= 0)
        {
            throw new AuthenticationException("Telegram login data is missing a user.");
        }

        fields.TryGetValue("first_name", out var firstName);
        fields.TryGetValue("username", out var username);
        return new TelegramLoginWidgetUser(
            telegramUserId,
            firstName ?? string.Empty,
            string.IsNullOrWhiteSpace(username) ? null : username,
            Convert.ToHexString(supplied).ToLowerInvariant());
    }

    private static bool TryParseHash(string? value, out byte[] hash)
    {
        hash = [];
        if (value is not { Length: HashHexLength })
        {
            return false;
        }

        try
        {
            hash = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
