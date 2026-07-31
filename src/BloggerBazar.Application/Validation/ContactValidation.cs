using System.Text.RegularExpressions;

namespace BloggerBazar.Application.Validation;

public static partial class ContactValidation
{
    public static bool IsUzbekPhone(string? value) => value is not null && UzbekPhone().IsMatch(value);
    public static bool IsTelegramUsername(string? value) => value is not null && TelegramUsername().IsMatch(value);
    public static bool IsInstagramUsername(string? value) => value is not null && InstagramUsername().IsMatch(value);
    public static bool IsHttpsUrl(string? value) => value is not null
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(uri.Host);

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
