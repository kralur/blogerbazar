namespace BloggerBazar.Application.Abstractions.Social;

// D68: what a platform says about a channel. Outcome is one of PlatformCheckOutcomes; Followers is set when verified.
public sealed record PlatformCheck(string Outcome, int? Followers = null);

public static class PlatformCheckOutcomes
{
    public const string Verified = "verified";
    // The channel does not exist (or is private / has no public link).
    public const string NotFound = "notFound";
    // Telegram: our bot is not an administrator of the channel, so Telegram does not tell us who owns it.
    public const string BotNotAdmin = "botNotAdmin";
    // Telegram: the person is not the creator or an administrator of the channel.
    public const string NotOwner = "notOwner";
    // YouTube: the verification code is not in the channel description.
    public const string CodeMissing = "codeMissing";
    // The server has no key for this platform (YouTube__ApiKey) or no bot token.
    public const string NotConfigured = "notConfigured";
    // The platform did not answer; try again later. Nothing changes.
    public const string Unavailable = "unavailable";
    // Instagram, TikTok: not checked automatically yet.
    public const string Unsupported = "unsupported";
}

public interface IPlatformStatsClient
{
    // ownerTelegramUserId: the person must be the creator or an administrator of the channel (checked on every read).
    Task<PlatformCheck> CheckTelegramChannelAsync(string channelUsername, long? ownerTelegramUserId, CancellationToken cancellationToken);

    // requiredCode: must appear in the channel description; null on a refresh (the code may be removed after proving).
    Task<PlatformCheck> CheckYouTubeChannelAsync(YouTubeChannelRef channel, string? requiredCode, CancellationToken cancellationToken);
}

// A YouTube channel by its @handle or its UC… id.
public sealed record YouTubeChannelRef(string? Handle, string? ChannelId);
