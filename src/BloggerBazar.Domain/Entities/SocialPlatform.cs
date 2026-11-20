namespace BloggerBazar.Domain.Entities;

public sealed class SocialPlatform
{
    private SocialPlatform() { }

    private SocialPlatform(Guid bloggerId, string type, string url, int? followers, string? screenshotUrl, int? averageReach, decimal? engagementRate)
    {
        Id = Guid.NewGuid();
        BloggerId = bloggerId;
        Type = type;
        Url = url;
        Followers = followers;
        ScreenshotUrl = screenshotUrl;
        AverageReach = averageReach;
        EngagementRate = engagementRate;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid BloggerId { get; private set; }
    public BloggerProfile Blogger { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string Url { get; private set; } = null!;
    public int? Followers { get; private set; }
    public string? ScreenshotUrl { get; private set; }
    public int? AverageReach { get; private set; }
    public decimal? EngagementRate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // D68: followers read from the platform itself (Telegram channel, YouTube channel) after the owner proved the
    // channel is theirs. While set, the blogger cannot type another number for it.
    public int? VerifiedFollowers { get; private set; }
    public string? VerificationSource { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }

    public bool IsVerified => VerificationSource is not null;

    public void MarkVerified(string source, int followers, DateTime nowUtc)
    {
        VerificationSource = source;
        VerifiedFollowers = followers;
        Followers = followers;
        VerifiedAtUtc = nowUtc;
    }

    public void ClearVerification()
    {
        VerificationSource = null;
        VerifiedFollowers = null;
        VerifiedAtUtc = null;
    }

    // The profile form recreates the platforms; the same channel keeps its verification and verified followers.
    public void CarryVerificationFrom(SocialPlatform previous)
    {
        if (!previous.IsVerified || previous.VerifiedFollowers is not { } followers || previous.VerifiedAtUtc is not { } verifiedAt) return;
        MarkVerified(previous.VerificationSource!, followers, verifiedAt);
    }

    public bool IsSameChannelAs(SocialPlatform other) =>
        string.Equals(Type, other.Type, StringComparison.OrdinalIgnoreCase) && string.Equals(NormalizedUrl(Url), NormalizedUrl(other.Url), StringComparison.OrdinalIgnoreCase);

    private static string NormalizedUrl(string url) => url.Trim().TrimEnd('/').Replace("://www.", "://", StringComparison.OrdinalIgnoreCase);

    public static SocialPlatform Create(Guid bloggerId, string type, string url, int? followers, string? screenshotUrl, int? averageReach = null, decimal? engagementRate = null) =>
        new(bloggerId, type, url, followers, screenshotUrl, averageReach, engagementRate);
}
