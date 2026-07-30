namespace BloggerBazar.Api.Contracts.BrandFaces;

public sealed record UpsertBrandFaceProfileRequest(
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
    string? AvatarUrl);
