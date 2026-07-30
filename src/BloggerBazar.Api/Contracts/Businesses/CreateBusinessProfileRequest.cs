namespace BloggerBazar.Api.Contracts.Businesses;

public sealed record CreateBusinessProfileRequest(string Name, string? Username, string? City, string? LogoUrl, string? WebsiteUrl, string? Description, string? Phone, string? Email);
