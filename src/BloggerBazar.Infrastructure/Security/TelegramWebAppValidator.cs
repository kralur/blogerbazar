using System.Security.Cryptography;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Security;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";
    public string BotToken { get; init; } = string.Empty;
    public string BotUsername { get; init; } = string.Empty;
    public string MiniAppUrl { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public int WebhookMaxBodyBytes { get; init; } = 65_536;
    public int MaxInitDataAgeSeconds { get; init; } = 3600;
    public int MaxInitDataClockSkewSeconds { get; init; } = 60;
}

internal sealed class TelegramWebAppValidator(IOptions<TelegramOptions> options) : ITelegramWebAppValidator
{
    public TelegramWebAppUser Validate(string initData)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.BotToken))
        {
            throw new InvalidOperationException("Telegram:BotToken is not configured.");
        }

        var values = QueryHelpers.ParseQuery(initData);
        if (!values.TryGetValue("hash", out var suppliedHash) || string.IsNullOrWhiteSpace(suppliedHash))
        {
            throw new AuthenticationException("Telegram initData is missing a hash.");
        }

        var dataCheckString = string.Join("\n", values
            .Where(pair => !string.Equals(pair.Key, "hash", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));

        using var secretKey = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secret = secretKey.ComputeHash(Encoding.UTF8.GetBytes(settings.BotToken));
        using var signature = new HMACSHA256(secret);
        var calculatedHash = Convert.ToHexString(signature.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(calculatedHash), Encoding.UTF8.GetBytes(suppliedHash.ToString())))
        {
            throw new AuthenticationException("Telegram initData signature is invalid.");
        }

        if (!values.TryGetValue("auth_date", out var authDate) || !long.TryParse(authDate, out var unixSeconds))
        {
            throw new AuthenticationException("Telegram initData has expired.");
        }

        var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var now = DateTimeOffset.UtcNow;
        if (authenticatedAt > now.AddSeconds(settings.MaxInitDataClockSkewSeconds)
            || now - authenticatedAt > TimeSpan.FromSeconds(settings.MaxInitDataAgeSeconds))
        {
            throw new AuthenticationException("Telegram initData has expired.");
        }

        if (!values.TryGetValue("user", out var userValue))
        {
            throw new AuthenticationException("Telegram initData is missing a user.");
        }

        var user = JsonSerializer.Deserialize<TelegramUserPayload>(userValue.ToString())
            ?? throw new AuthenticationException("Telegram user payload is invalid.");
        return new TelegramWebAppUser(user.Id, user.Username, user.FirstName);
    }

    private sealed record TelegramUserPayload(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("first_name")] string FirstName);
}
