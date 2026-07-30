using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Features.Businesses;

public sealed record BusinessProfileDto(Guid Id, string Name, string? Username, string? City, string? LogoUrl, string? WebsiteUrl, string? Description, bool IsVerified, int ModerationStatus, DateTime CreatedAtUtc)
{
    public static BusinessProfileDto From(BusinessProfile profile) => new(profile.Id, profile.Name, profile.Username, profile.City, profile.LogoUrl, profile.WebsiteUrl, profile.Description, profile.IsVerified, (int)profile.ModerationStatus, profile.CreatedAtUtc);
}
