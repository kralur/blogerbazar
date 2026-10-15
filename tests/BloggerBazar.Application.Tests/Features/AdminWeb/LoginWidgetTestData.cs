using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BloggerBazar.Application.Tests.Features.AdminWeb;

// Telegram Login Widget data signed the way Telegram signs it (https://core.telegram.org/widgets/login).
internal static class LoginWidgetTestData
{
    public static Dictionary<string, string> Signed(string botToken, long telegramUserId, DateTimeOffset? authDate = null, IReadOnlyDictionary<string, string>? extra = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = telegramUserId.ToString(CultureInfo.InvariantCulture),
            ["first_name"] = "Umid",
            ["username"] = "umid",
            ["auth_date"] = (authDate ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
        };
        foreach (var (key, value) in extra ?? new Dictionary<string, string>())
        {
            fields[key] = value;
        }

        fields["hash"] = Sign(botToken, fields);
        return fields;
    }

    public static string Sign(string botToken, IReadOnlyDictionary<string, string> fields)
    {
        var dataCheckString = string.Join('\n', fields
            .Where(pair => pair.Key != "hash")
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        return Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
    }
}
