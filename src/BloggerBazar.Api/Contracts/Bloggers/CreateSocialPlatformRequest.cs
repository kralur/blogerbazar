namespace BloggerBazar.Api.Contracts.Bloggers;

public sealed record CreateSocialPlatformRequest(string Type, string Url, int? Followers, string? ScreenshotUrl, int? AverageReach = null, decimal? EngagementRate = null);
