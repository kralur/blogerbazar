using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Admin;

public sealed record AdminDashboardDto(
    int Users,
    int Bloggers,
    int Businesses,
    int PublishedCampaigns,
    int CompletedDeals,
    int PromotedBloggers,
    int PromotedCampaigns);

public sealed record GetAdminDashboardQuery(long TelegramUserId) : IRequest<AdminDashboardDto>;

public sealed class GetAdminDashboardHandler(
    IAdminAccessPolicy access,
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICampaignRepository campaigns) : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.TelegramUserId);
        var bloggerProfiles = await bloggers.GetAllAsync(500, cancellationToken);
        var businessProfiles = await businesses.GetAllAsync(500, cancellationToken);
        var allCampaigns = await campaigns.GetAllAsync(500, cancellationToken);
        return new AdminDashboardDto(
            await users.CountActiveAsync(cancellationToken),
            bloggerProfiles.Count,
            businessProfiles.Count,
            allCampaigns.Count(campaign => campaign.Status == CampaignStatus.Published),
            bloggerProfiles.Sum(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed)),
            bloggerProfiles.Count(profile => profile.IsPromoted),
            allCampaigns.Count(campaign => campaign.IsPromoted));
    }
}
