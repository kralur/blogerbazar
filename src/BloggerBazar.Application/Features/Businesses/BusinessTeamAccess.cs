using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Features.Businesses;

// D64: "my company" for a request is the company of the caller's team membership. Null when the caller is in no
// company (the handler answers as before: empty, 404 or "create a profile first"); 403 when their role in the
// team does not allow the action.
// D26: fixed camelCase codes the Mini App translates.
public static class BusinessTeamCodes
{
    public const string MemberOfAnotherCompany = "business_member_elsewhere";
}

internal static class BusinessTeamAccess
{
    internal static async Task<BusinessProfile?> FindAsync(
        IBusinessProfileRepository businesses,
        long telegramUserId,
        BusinessPermission permission,
        CancellationToken cancellationToken)
    {
        var membership = await businesses.GetMembershipAsync(telegramUserId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        return membership.Allows(permission)
            ? membership.Business
            : throw new UnauthorizedAccessException("The caller's role in the company does not allow this action.");
    }
}
