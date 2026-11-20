using System.Net;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Social;
using BloggerBazar.Infrastructure.Security;
using BloggerBazar.Infrastructure.Telegram;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Social;

public sealed class YouTubeOptions
{
    public const string SectionName = "YouTube";
    // YouTube Data API v3 key (Railway variable YouTube__ApiKey). Without it YouTube channels are not checked.
    public string ApiKey { get; init; } = string.Empty;
}

// D67: reads a Telegram channel through our bot (it must be an administrator there) and a YouTube channel through the
// YouTube Data API. Any failure of the platform is "unavailable" and changes nothing.
internal sealed class PlatformStatsClient(HttpClient httpClient, IOptions<TelegramOptions> telegram, IOptions<YouTubeOptions> youTube, ILogger<PlatformStatsClient>? logger = null) : IPlatformStatsClient
{
    private static readonly Uri TelegramBase = new("https://api.telegram.org/");

    public async Task<PlatformCheck> CheckTelegramChannelAsync(string channelUsername, long? ownerTelegramUserId, CancellationToken cancellationToken)
    {
        var token = telegram.Value.BotToken;
        if (string.IsNullOrWhiteSpace(token)) return new PlatformCheck(PlatformCheckOutcomes.NotConfigured);
        try
        {
            var chat = Uri.EscapeDataString("@" + channelUsername);
            // Administrators are listed only to an administrator: this also proves our bot is one.
            using var admins = await GetTelegramAsync(token, $"getChatAdministrators?chat_id={chat}", cancellationToken);
            if (admins.Outcome is { } failed) return new PlatformCheck(failed);
            if (ownerTelegramUserId is { } owner && !admins.Result.EnumerateArray().Any(admin =>
                    admin.TryGetProperty("user", out var user) && user.TryGetProperty("id", out var id) && id.GetInt64() == owner
                    && admin.TryGetProperty("status", out var status) && status.GetString() is "creator" or "administrator"))
                return new PlatformCheck(PlatformCheckOutcomes.NotOwner);
            using var count = await GetTelegramAsync(token, $"getChatMemberCount?chat_id={chat}", cancellationToken);
            if (count.Outcome is { } countFailed) return new PlatformCheck(countFailed);
            return new PlatformCheck(PlatformCheckOutcomes.Verified, count.Result.GetInt32());
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException)
        {
            logger?.LogWarning(exception, "Telegram channel check failed");
            return new PlatformCheck(PlatformCheckOutcomes.Unavailable);
        }
    }

    public async Task<PlatformCheck> CheckYouTubeChannelAsync(YouTubeChannelRef channel, string? requiredCode, CancellationToken cancellationToken)
    {
        var key = youTube.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) return new PlatformCheck(PlatformCheckOutcomes.NotConfigured);
        try
        {
            var selector = channel.ChannelId is { } id ? $"id={Uri.EscapeDataString(id)}" : $"forHandle={Uri.EscapeDataString(channel.Handle ?? "")}";
            using var response = await httpClient.GetAsync($"https://www.googleapis.com/youtube/v3/channels?part=snippet,statistics&{selector}&key={Uri.EscapeDataString(key)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger?.LogWarning("YouTube channel check answered {Status}", (int)response.StatusCode);
                return new PlatformCheck(PlatformCheckOutcomes.Unavailable);
            }
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0) return new PlatformCheck(PlatformCheckOutcomes.NotFound);
            var item = items[0];
            var description = item.TryGetProperty("snippet", out var snippet) && snippet.TryGetProperty("description", out var text) ? text.GetString() ?? "" : "";
            if (requiredCode is not null && !description.Contains(requiredCode, StringComparison.OrdinalIgnoreCase)) return new PlatformCheck(PlatformCheckOutcomes.CodeMissing);
            var statistics = item.GetProperty("statistics");
            if (statistics.TryGetProperty("hiddenSubscriberCount", out var hidden) && hidden.GetBoolean()) return new PlatformCheck(PlatformCheckOutcomes.Unavailable);
            var subscribers = long.Parse(statistics.GetProperty("subscriberCount").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            return new PlatformCheck(PlatformCheckOutcomes.Verified, (int)Math.Min(subscribers, int.MaxValue));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            logger?.LogWarning(exception, "YouTube channel check failed");
            return new PlatformCheck(PlatformCheckOutcomes.Unavailable);
        }
    }

    // ok → Result; "chat not found" → NotFound; other 400/403 (member list inaccessible, not an administrator) → BotNotAdmin.
    private async Task<TelegramAnswer> GetTelegramAsync(string token, string methodAndQuery, CancellationToken cancellationToken)
    {
        var parts = methodAndQuery.Split('?', 2);
        var uri = new Uri(TelegramBase, TelegramBotApi.MethodUri(token, parts[0]) + "?" + parts[1]);
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean()) return new TelegramAnswer(document, root.GetProperty("result"), null);
        var description = root.TryGetProperty("description", out var text) ? text.GetString() ?? "" : "";
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound && description.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) || response.StatusCode == HttpStatusCode.Unauthorized)
            return new TelegramAnswer(document, default, PlatformCheckOutcomes.NotConfigured);
        if (response.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError) return new TelegramAnswer(document, default, PlatformCheckOutcomes.Unavailable);
        return new TelegramAnswer(document, default, description.Contains("not found", StringComparison.OrdinalIgnoreCase) ? PlatformCheckOutcomes.NotFound : PlatformCheckOutcomes.BotNotAdmin);
    }

    private sealed record TelegramAnswer(JsonDocument Document, JsonElement Result, string? Outcome) : IDisposable
    {
        public void Dispose() => Document.Dispose();
    }
}
