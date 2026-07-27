namespace BloggerBazar.Domain.Entities;

public sealed class SocialPlatform
{
    private SocialPlatform() { }

    private SocialPlatform(Guid bloggerId, string type, string url, int? followers, string? screenshotUrl)
    {
        Id = Guid.NewGuid();
        BloggerId = bloggerId;
        Type = type;
        Url = url;
        Followers = followers;
        ScreenshotUrl = screenshotUrl;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid BloggerId { get; private set; }
    public BloggerProfile Blogger { get; private set; } = null!;
    public string Type { get; private set; } = null!;
    public string Url { get; private set; } = null!;
    public int? Followers { get; private set; }
    public string? ScreenshotUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static SocialPlatform Create(Guid bloggerId, string type, string url, int? followers, string? screenshotUrl) =>
        new(bloggerId, type, url, followers, screenshotUrl);
}
