using System.Linq.Expressions;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Infrastructure.Persistence;

internal static class DealParticipantFilter
{
    // Reading: the deal stays with its participant even after the partner deletes the account (D43).
    public static Expression<Func<Deal, bool>> Visible(MarketplaceRole role, Guid profileId) => role switch
    {
        MarketplaceRole.Blogger => deal => deal.BloggerId == profileId && !deal.Blogger!.IsDeleted,
        MarketplaceRole.Business => deal => deal.BusinessId == profileId && !deal.Business.IsDeleted,
        MarketplaceRole.BrandFace => deal => deal.BrandFaceId == profileId && !deal.BrandFace!.IsDeleted,
        _ => deal => false
    };

    // Acting (complete, review, contacts): both sides must still exist; a deleted partner reads as missing.
    public static Expression<Func<Deal, bool>> For(MarketplaceRole role, Guid profileId) => role switch
    {
        MarketplaceRole.Blogger => deal => deal.BloggerId == profileId && !deal.Blogger!.IsDeleted && !deal.Business.IsDeleted,
        MarketplaceRole.BrandFace => deal => deal.BrandFaceId == profileId && !deal.BrandFace!.IsDeleted && !deal.Business.IsDeleted,
        MarketplaceRole.Business => deal => deal.BusinessId == profileId && !deal.Business.IsDeleted
            && (deal.BloggerId != null ? !deal.Blogger!.IsDeleted : !deal.BrandFace!.IsDeleted),
        _ => deal => false
    };
}
