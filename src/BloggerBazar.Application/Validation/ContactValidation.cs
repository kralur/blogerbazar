using System.Text.RegularExpressions;

namespace BloggerBazar.Application.Validation;

public static partial class ContactValidation
{
    public static bool IsUzbekPhone(string? value) => value is not null && UzbekPhone().IsMatch(value);

    // Telegram sends a contact's number as digits, sometimes with "+". Uzbek numbers get the usual spacing;
    // any other country is accepted as an international number (D41). Null when it is not a phone number.
    public static string? NormalizeTelegramPhone(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is < 7 or > 15) return null;
        return digits.Length == 12 && digits.StartsWith("998", StringComparison.Ordinal)
            ? $"+998 {digits[3..5]} {digits[5..8]} {digits[8..10]} {digits[10..12]}"
            : $"+{digits}";
    }

    public static bool IsVerifiedPhone(string? value) => value is not null && NormalizeTelegramPhone(value) == value;
    public static bool IsTelegramUsername(string? value) => value is not null && TelegramUsername().IsMatch(value);

    // The handle shown as a contact comes only from Telegram's signed launch data, never from the form,
    // so nobody can put someone else's @username on their profile. No usable username means no handle.
    public static string? TelegramHandle(string? telegramUsername)
    {
        if (string.IsNullOrWhiteSpace(telegramUsername)) return null;
        var handle = $"@{telegramUsername.Trim().TrimStart('@')}";
        return IsTelegramUsername(handle) ? handle : null;
    }
    public static bool IsInstagramUsername(string? value) => value is not null && InstagramUsername().IsMatch(value);
    public static bool IsHttpsUrl(string? value) => value is not null
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(uri.Host)
        // "https://bank.uz@evil.com" shows a trusted name but opens another host.
        && string.IsNullOrEmpty(uri.UserInfo);

    public static bool IsSupportedPlatform(string? type, string? url) => type?.Trim().ToLowerInvariant() switch
    {
        "instagram" or "telegram" or "tiktok" or "youtube" => IsHttpsUrl(url),
        _ => false
    };

    [GeneratedRegex("^\\+998\\s?\\d{2}\\s?\\d{3}\\s?\\d{2}\\s?\\d{2}$")]
    private static partial Regex UzbekPhone();

    [GeneratedRegex("^@[A-Za-z0-9_]{5,32}$")]
    private static partial Regex TelegramUsername();

    [GeneratedRegex("^@[A-Za-z0-9._]{1,30}$")]
    private static partial Regex InstagramUsername();
}
