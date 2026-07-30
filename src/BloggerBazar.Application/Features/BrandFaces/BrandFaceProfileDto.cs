using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Features.BrandFaces;

public sealed record BrandFaceProfileDto(
    Guid Id,
    string Name,
    string City,
    int? Age,
    string? Gender,
    IReadOnlyCollection<string> Languages,
    IReadOnlyCollection<string> Categories,
    string? Experience,
    string? Instagram,
    string? Telegram,
    string? PortfolioUrl,
    int? CollaborationPrice,
    string? Description,
    string? AvatarUrl,
    bool IsPromoted)
{
    public static BrandFaceProfileDto From(BrandFaceProfile profile) => new(
        profile.Id, profile.Name, profile.City, profile.Age, profile.Gender, profile.Languages,
        profile.Categories, profile.Experience, profile.Instagram, profile.Telegram,
        profile.PortfolioUrl, profile.CollaborationPrice, profile.Description, profile.AvatarUrl, profile.IsPromoted);
}
