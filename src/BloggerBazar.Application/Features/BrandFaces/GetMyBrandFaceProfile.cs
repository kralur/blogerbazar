using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;

namespace BloggerBazar.Application.Features.BrandFaces;

public sealed record GetMyBrandFaceProfileQuery(long TelegramUserId) : IRequest<BrandFaceProfileDto?>;

public sealed class GetMyBrandFaceProfileHandler(IBrandFaceProfileRepository profiles)
    : IRequestHandler<GetMyBrandFaceProfileQuery, BrandFaceProfileDto?>
{
    public async Task<BrandFaceProfileDto?> Handle(GetMyBrandFaceProfileQuery query, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        return profile is null ? null : BrandFaceProfileDto.From(profile);
    }
}
