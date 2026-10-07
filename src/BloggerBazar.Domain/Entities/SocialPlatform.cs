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

    public static SocialPlatform Create(Guid bloggerId, string type, string url, int? followers, string? screenshotUrl, int? averageReach = null, decimal? engagementRate = null) =>
        new(bloggerId, type, url, followers, screenshotUrl, averageReach, engagementRate);
}
