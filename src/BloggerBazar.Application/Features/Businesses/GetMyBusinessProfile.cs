using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using MediatR;

namespace BloggerBazar.Application.Features.Businesses;

public sealed record GetMyBusinessProfileQuery(long TelegramUserId) : IRequest<MyBusinessProfileDto?>;

public sealed record MyBusinessProfileDto(
    Guid Id,
    string Name,
    string? Username,
    string? City,
    string? LogoUrl,
    string? Description,
    string? Phone,
    string? Email,
    bool IsVerified)
{
    public static MyBusinessProfileDto From(BusinessProfile profile) => new(
        profile.Id,
        profile.Name,
        profile.Username,
        profile.City,
        profile.LogoUrl,
        profile.Description,
        profile.Phone,
        profile.Email,
        profile.IsVerified);
}

public sealed class GetMyBusinessProfileHandler(IBusinessProfileRepository businesses)
    : IRequestHandler<GetMyBusinessProfileQuery, MyBusinessProfileDto?>
{
    public async Task<MyBusinessProfileDto?> Handle(GetMyBusinessProfileQuery query, CancellationToken cancellationToken)
    {
        var profile = await businesses.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        return profile is null ? null : MyBusinessProfileDto.From(profile);
    }
}
