using System.Linq.Expressions;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Infrastructure.Persistence;

internal static class DealParticipantFilter
{
    public static Expression<Func<Deal, bool>> For(MarketplaceRole role, Guid profileId) => role switch
    {
        MarketplaceRole.Blogger => deal => deal.BloggerId == profileId && !deal.Blogger.IsDeleted && !deal.Business.IsDeleted,
        MarketplaceRole.Business => deal => deal.BusinessId == profileId && !deal.Blogger.IsDeleted && !deal.Business.IsDeleted,
        _ => deal => false
    };
}
