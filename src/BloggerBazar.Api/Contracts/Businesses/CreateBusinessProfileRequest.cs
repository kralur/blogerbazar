namespace BloggerBazar.Api.Contracts.Businesses;

// TeamMode (D64 stage C, additive): "with a team" chosen when the company is created; omitted keeps it alone.
public sealed record CreateBusinessProfileRequest(string Name, string? Username, string? City, string? LogoUrl, string? WebsiteUrl, string? Description, string? Phone, string? Email, bool? TeamMode = null);
