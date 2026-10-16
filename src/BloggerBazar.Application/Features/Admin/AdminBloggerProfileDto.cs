using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Features.Admin;

// TelegramUserId (added for the web admin) opens the owner's admin card.
public sealed record AdminBloggerProfileDto(Guid Id, string Name, string City, IReadOnlyCollection<string> Categories, string? AvatarUrl, int TotalFollowers, int Status, DateTime CreatedAtUtc, long? TelegramUserId = null)
{
    public static AdminBloggerProfileDto From(BloggerProfile profile) =>
        new(profile.Id, profile.Name, profile.City, profile.Categories, profile.AvatarUrl, profile.TotalFollowers, (int)profile.Status, profile.CreatedAtUtc, profile.TelegramUserId);
}
