using BloggerBazar.Domain.Enums;
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
    string? WebsiteUrl,
    string? Description,
    string? Phone,
    string? Email,
    bool IsVerified,
    int ModerationStatus,
    // D64 stage C (additive): the company works with a team, and the caller's role in it (D26 string).
    bool TeamMode = false,
    string? TeamRole = null)
{
    public static MyBusinessProfileDto From(BusinessProfile profile) => new(
        profile.Id,
        profile.Name,
        profile.Username,
        profile.City,
        profile.LogoUrl,
        profile.WebsiteUrl,
        profile.Description,
        profile.Phone,
        profile.Email,
        profile.IsVerified,
        (int)profile.ModerationStatus);
}

public sealed class GetMyBusinessProfileHandler(IBusinessProfileRepository businesses)
    : IRequestHandler<GetMyBusinessProfileQuery, MyBusinessProfileDto?>
{
    public async Task<MyBusinessProfileDto?> Handle(GetMyBusinessProfileQuery query, CancellationToken cancellationToken)
    {
        var membership = await businesses.GetMembershipAsync(query.TelegramUserId, cancellationToken);
        return membership is null ? null : MyBusinessProfileDto.From(membership.Business) with
        {
            TeamMode = membership.Business.TeamMode,
            TeamRole = Team.TeamRoles.ToApi(membership.Role)
        };
    }
}
