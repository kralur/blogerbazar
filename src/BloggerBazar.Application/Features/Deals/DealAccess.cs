using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Features.Deals;

internal sealed record DealParticipantContext(MarketplaceRole Role, Guid ProfileId, BusinessMemberRole? BusinessRole = null)
{
    // The creator side of a deal: a blogger or a brand face (D46).
    public bool IsCreator => Role is MarketplaceRole.Blogger or MarketplaceRole.BrandFace;
}

internal static class DealAccess
{
    // Rule 1 with teams (D64): the selected role is Business and the caller is a member of a company whose role allows
    // the action. Most callers change something, so the default is Operate.
    internal static async Task<BusinessProfile> RequireBusinessAsync(
        IPlatformUserRepository users,
        IBusinessProfileRepository businesses,
        long telegramUserId,
        CancellationToken cancellationToken,
        BusinessPermission permission = BusinessPermission.Operate)
    {
        var user = await RequireActiveUserAsync(users, telegramUserId, cancellationToken);
        if (user.SelectedMarketplaceRole != MarketplaceRole.Business)
        {
            throw new UnauthorizedAccessException("An active business marketplace role is required.");
        }

        var membership = await businesses.GetMembershipAsync(telegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("An active business profile is required.");
        return membership.Allows(permission)
            ? membership.Business
            : throw new UnauthorizedAccessException("The caller's role in the company does not allow this action.");
    }

    // A deal participant that changes something on the business side needs a working role (not a viewer).
    internal static void EnsureCanOperate(DealParticipantContext participant)
    {
        if (participant.BusinessRole is { } role && !BusinessMember.Allows(role, BusinessPermission.Operate))
        {
            throw new UnauthorizedAccessException("The caller's role in the company does not allow this action.");
        }
    }

    // Null means the account has no deal side for its selected role; callers answer [] or 404.
    internal static async Task<DealParticipantContext?> FindDealParticipantAsync(
        IPlatformUserRepository users,
        IBloggerProfileRepository bloggers,
        IBusinessProfileRepository businesses,
        long telegramUserId,
        CancellationToken cancellationToken,
        IBrandFaceProfileRepository? brandFaces = null)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted)
        {
            return null;
        }

        return user.SelectedMarketplaceRole switch
        {
            MarketplaceRole.Blogger => await bloggers.GetByTelegramUserIdAsync(telegramUserId, cancellationToken) is { } blogger
                ? new DealParticipantContext(MarketplaceRole.Blogger, blogger.Id)
                : null,
            MarketplaceRole.Business => await businesses.GetMembershipAsync(telegramUserId, cancellationToken) is { } membership
                ? new DealParticipantContext(MarketplaceRole.Business, membership.Business.Id, membership.Role)
                : null,
            // D46: a brand face is a deal's creator side exactly like a blogger.
            MarketplaceRole.BrandFace => brandFaces is not null && await brandFaces.GetByTelegramUserIdAsync(telegramUserId, cancellationToken) is { } brandFace
                ? new DealParticipantContext(MarketplaceRole.BrandFace, brandFace.Id)
                : null,
            _ => null
        };
    }

    internal static InvalidOperationException DealNotFound() => new("Deal was not found.");

    private static async Task<PlatformUser> RequireActiveUserAsync(
        IPlatformUserRepository users,
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
        if (user is null || user.IsBlocked || user.IsDeleted || user.SelectedMarketplaceRole is null)
        {
            throw new UnauthorizedAccessException("An active marketplace account is required.");
        }

        return user;
    }
}
