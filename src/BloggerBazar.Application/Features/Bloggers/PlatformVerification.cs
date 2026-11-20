using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Social;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Bloggers;

// D67: followers confirmed by the platform itself. A Telegram channel is proved by our bot being its administrator
// and the blogger being its creator or an administrator; a YouTube channel by a code in its description. The number
// then comes from the platform and is refreshed by a button and weekly.
public static partial class PlatformVerification
{
    public const string Telegram = "telegram";
    public const string YouTube = "youtube";
    // A button press within this time returns the saved numbers without asking the platform again.
    public static readonly TimeSpan RecheckAfter = TimeSpan.FromMinutes(10);
    // Verified numbers older than this are read again by the weekly refresh.
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromDays(7);
    // A platform that has not answered for this long loses the mark instead of showing old numbers forever.
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    // "BB-" and six letters/digits derived from the platform id: the same code every time for the same channel entry.
    public static string CodeFor(Guid platformId)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("bloggerbazar-platform:" + platformId.ToString("N")));
        var code = new StringBuilder("BB-");
        for (var index = 0; index < 6; index++) code.Append(alphabet[hash[index] % alphabet.Length]);
        return code.ToString();
    }

    // https://t.me/name (or telegram.me/name) → name. Private invite links (t.me/+…, joinchat) have no public name.
    public static string? TelegramChannel(string url)
    {
        var match = TelegramLink().Match(url.Trim());
        return match.Success ? match.Groups["name"].Value : null;
    }

    // https://youtube.com/@handle, /channel/UC…; /c/… and /user/… old names cannot be looked up reliably.
    public static YouTubeChannelRef? YouTubeChannel(string url)
    {
        var match = YouTubeLink().Match(url.Trim());
        if (!match.Success) return null;
        return match.Groups["handle"].Success ? new YouTubeChannelRef("@" + match.Groups["handle"].Value, null) : new YouTubeChannelRef(null, match.Groups["id"].Value);
    }

    public static bool CanVerify(string type) => type.Equals(Telegram, StringComparison.OrdinalIgnoreCase) || type.Equals(YouTube, StringComparison.OrdinalIgnoreCase);

    // One platform. Telegram ownership is checked every time (an administrator who lost the rights loses the mark);
    // the YouTube code only when proving (refresh=false): it may be removed afterwards.
    public static async Task<PlatformCheck> CheckAsync(IPlatformStatsClient client, SocialPlatform platform, long ownerTelegramUserId, bool refresh, CancellationToken cancellationToken)
    {
        if (platform.Type.Equals(Telegram, StringComparison.OrdinalIgnoreCase))
        {
            return TelegramChannel(platform.Url) is { } channel
                ? await client.CheckTelegramChannelAsync(channel, ownerTelegramUserId, cancellationToken)
                : new PlatformCheck(PlatformCheckOutcomes.NotFound);
        }
        if (platform.Type.Equals(YouTube, StringComparison.OrdinalIgnoreCase))
        {
            return YouTubeChannel(platform.Url) is { } channel
                ? await client.CheckYouTubeChannelAsync(channel, refresh ? null : CodeFor(platform.Id), cancellationToken)
                : new PlatformCheck(PlatformCheckOutcomes.NotFound);
        }
        return new PlatformCheck(PlatformCheckOutcomes.Unsupported);
    }

    // The profile total follows the platforms (the form sends the sum of their followers).
    internal static int TotalFollowers(IEnumerable<SocialPlatform> platforms, SocialPlatform changed) =>
        platforms.Sum(platform => (platform.Id == changed.Id ? changed.Followers : platform.Followers) ?? 0);

    [GeneratedRegex(@"^https?://(?:www\.)?(?:t|telegram)\.me/(?<name>[A-Za-z][A-Za-z0-9_]{3,31})/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TelegramLink();

    [GeneratedRegex(@"^https?://(?:www\.|m\.)?youtube\.com/(?:@(?<handle>[A-Za-z0-9._-]{3,30})|channel/(?<id>UC[A-Za-z0-9_-]{22}))/?(?:[?#].*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex YouTubeLink();
}

public sealed record VerifyMyPlatformCommand(long TelegramUserId, Guid PlatformId) : IRequest<PlatformVerificationResultDto>;

// outcome: PlatformCheckOutcomes; code: the YouTube code to put in the channel description (only for YouTube);
// botUsername: the bot to add as a channel administrator (Telegram), filled in by the API.
public sealed record PlatformVerificationResultDto(string Outcome, SocialPlatformDto Platform, string? Code = null, string? BotUsername = null);

public sealed class VerifyMyPlatformHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    ISocialPlatformRepository platforms,
    IPlatformStatsClient client,
    IUnitOfWork unitOfWork,
    ICatalogCache? cache = null) : IRequestHandler<VerifyMyPlatformCommand, PlatformVerificationResultDto>
{
    public async Task<PlatformVerificationResultDto> Handle(VerifyMyPlatformCommand command, CancellationToken cancellationToken)
    {
        // Rule 1: the selected Blogger role and its own profile; rule 2: someone else's platform is simply missing.
        var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole != MarketplaceRole.Blogger)
            throw new UnauthorizedAccessException("An active blogger marketplace role is required.");
        var blogger = await bloggers.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Platform not found.");
        var platform = await platforms.GetForBloggerAsync(blogger.Id, command.PlatformId, cancellationToken)
            ?? throw new InvalidOperationException("Platform not found.");
        var code = platform.Type.Equals(PlatformVerification.YouTube, StringComparison.OrdinalIgnoreCase) ? PlatformVerification.CodeFor(platform.Id) : null;
        if (!PlatformVerification.CanVerify(platform.Type))
            return new PlatformVerificationResultDto(PlatformCheckOutcomes.Unsupported, SocialPlatformDto.From(platform));

        var now = DateTime.UtcNow;
        if (platform.IsVerified && platform.VerifiedAtUtc > now - PlatformVerification.RecheckAfter)
            return new PlatformVerificationResultDto(PlatformCheckOutcomes.Verified, SocialPlatformDto.From(platform), code);

        // An already verified channel only reads its numbers again; a new one proves ownership first.
        var check = await PlatformVerification.CheckAsync(client, platform, command.TelegramUserId, refresh: platform.IsVerified, cancellationToken);
        if (check.Outcome == PlatformCheckOutcomes.Verified && check.Followers is { } followers)
        {
            var all = await platforms.ListForBloggerAsync(blogger.Id, cancellationToken);
            platform.MarkVerified(platform.Type.ToLowerInvariant(), followers, now);
            blogger.SetTotalFollowers(PlatformVerification.TotalFollowers(all, platform));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        }
        else if (platform.IsVerified && check.Outcome is PlatformCheckOutcomes.NotFound or PlatformCheckOutcomes.BotNotAdmin or PlatformCheckOutcomes.NotOwner)
        {
            // The channel is gone or our bot was removed: the mark goes, the last number stays as the blogger's own.
            platform.ClearVerification();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        }
        return new PlatformVerificationResultDto(check.Outcome, SocialPlatformDto.From(platform), code);
    }
}

// The weekly refresh (PlatformVerificationWorker): verified numbers older than a week are read again.
public sealed record RefreshVerifiedPlatformsCommand(int Take = 50) : IRequest<int>;

public sealed class RefreshVerifiedPlatformsHandler(
    ISocialPlatformRepository platforms,
    IPlatformStatsClient client,
    IUnitOfWork unitOfWork,
    ICatalogCache? cache = null) : IRequestHandler<RefreshVerifiedPlatformsCommand, int>
{
    public async Task<int> Handle(RefreshVerifiedPlatformsCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var due = await platforms.ListVerifiedBeforeAsync(now - PlatformVerification.RefreshAfter, command.Take, cancellationToken);
        var changed = 0;
        foreach (var platform in due)
        {
            var check = await PlatformVerification.CheckAsync(client, platform, platform.Blogger.TelegramUserId, refresh: true, cancellationToken);
            if (check.Outcome == PlatformCheckOutcomes.Verified && check.Followers is { } followers)
            {
                var all = await platforms.ListForBloggerAsync(platform.BloggerId, cancellationToken);
                platform.MarkVerified(platform.VerificationSource ?? platform.Type.ToLowerInvariant(), followers, now);
                platform.Blogger.SetTotalFollowers(PlatformVerification.TotalFollowers(all, platform));
                changed++;
            }
            else if (check.Outcome is PlatformCheckOutcomes.NotFound or PlatformCheckOutcomes.BotNotAdmin or PlatformCheckOutcomes.NotOwner
                     || platform.VerifiedAtUtc < now - PlatformVerification.MaxAge)
            {
                // Gone, no longer ours, or silent for a month: the mark goes, the last number stays as the blogger's own.
                platform.ClearVerification();
                changed++;
            }
            // Unavailable or not configured, and recent: try again next time.
        }
        if (changed > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        }
        return changed;
    }
}
