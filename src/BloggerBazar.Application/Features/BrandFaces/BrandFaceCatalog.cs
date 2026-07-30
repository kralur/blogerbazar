using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;

namespace BloggerBazar.Application.Features.BrandFaces;

public sealed record BrandFacePublicDto(
    Guid Id, string Name, string City, IReadOnlyCollection<string> Languages, IReadOnlyCollection<string> Categories,
    string? Experience, string? Instagram, string? PortfolioUrl, int? CollaborationPrice, string? Description,
    string? AvatarUrl, bool IsPromoted, DateTime CreatedAtUtc)
{
    public static BrandFacePublicDto From(BloggerBazar.Domain.Entities.BrandFaceProfile profile) => new(
        profile.Id, profile.Name, profile.City, profile.Languages, profile.Categories, profile.Experience,
        profile.Instagram, profile.PortfolioUrl, profile.CollaborationPrice, profile.Description, profile.AvatarUrl,
        profile.IsPromoted, profile.CreatedAtUtc);
}

public sealed record SearchBrandFacesQuery(string? Query, string? City, string? Category, int Page = 1, int PageSize = 20) : IRequest<IReadOnlyList<BrandFacePublicDto>>;
public sealed record GetBrandFaceQuery(Guid Id) : IRequest<BrandFacePublicDto?>;

public sealed class SearchBrandFacesHandler(IBrandFaceProfileRepository profiles) : IRequestHandler<SearchBrandFacesQuery, IReadOnlyList<BrandFacePublicDto>>
{
    public async Task<IReadOnlyList<BrandFacePublicDto>> Handle(SearchBrandFacesQuery query, CancellationToken cancellationToken) =>
        (await profiles.SearchAsync(query.Query, query.City, query.Category, (Math.Max(query.Page, 1) - 1) * Math.Clamp(query.PageSize, 1, 50), Math.Clamp(query.PageSize, 1, 50), cancellationToken)).Select(BrandFacePublicDto.From).ToArray();
}

public sealed class GetBrandFaceHandler(IBrandFaceProfileRepository profiles) : IRequestHandler<GetBrandFaceQuery, BrandFacePublicDto?>
{
    public async Task<BrandFacePublicDto?> Handle(GetBrandFaceQuery query, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByIdAsync(query.Id, cancellationToken);
        return profile is null ? null : BrandFacePublicDto.From(profile);
    }
}
