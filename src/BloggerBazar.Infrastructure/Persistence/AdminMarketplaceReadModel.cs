using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class AdminMarketplaceReadModel(BloggerBazarDbContext dbContext) : IAdminMarketplaceReadModel
{
    public async Task<IReadOnlyList<AdminBloggerProfileDto>> GetBloggersAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BloggerProfiles.AsNoTracking().OrderByDescending(profile => profile.CreatedAtUtc).Take(take)
            .Select(profile => new AdminBloggerProfileDto(profile.Id, profile.Name, profile.City, profile.Categories,
                profile.AvatarUrl, profile.TotalFollowers, (int)profile.Status, profile.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminCampaignDto>> GetCampaignsAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.Campaigns.AsNoTracking().OrderByDescending(campaign => campaign.CreatedAtUtc).Take(take)
            .Select(campaign => new AdminCampaignDto(campaign.Id, campaign.Title, campaign.Business.Name, (int)campaign.Status,
                campaign.IsPromoted, campaign.Applications.Count, campaign.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequestsAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.CollaborationRequests.AsNoTracking().OrderByDescending(request => request.CreatedAtUtc).Take(take)
            .Select(request => new CollaborationRequestDto(request.Id, request.BloggerId, request.Blogger.Name, request.BusinessId,
                request.Business.Name, request.Message, (int)request.Status, request.Deal == null ? null : request.Deal.Id, request.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var users = dbContext.PlatformUsers.AsNoTracking().CountAsync(user => !user.IsBlocked && !user.IsDeleted, cancellationToken);
        var bloggers = dbContext.BloggerProfiles.AsNoTracking().CountAsync(cancellationToken);
        var businesses = dbContext.BusinessProfiles.AsNoTracking().CountAsync(cancellationToken);
        var publishedCampaigns = dbContext.Campaigns.AsNoTracking().CountAsync(campaign => campaign.Status == CampaignStatus.Published, cancellationToken);
        var completedDeals = dbContext.Deals.AsNoTracking().CountAsync(deal => deal.Status == DealStatus.Completed, cancellationToken);
        var promotedBloggers = dbContext.BloggerProfiles.AsNoTracking().CountAsync(profile => profile.IsPromoted, cancellationToken);
        var promotedCampaigns = dbContext.Campaigns.AsNoTracking().CountAsync(campaign => campaign.IsPromoted, cancellationToken);

        await Task.WhenAll(users, bloggers, businesses, publishedCampaigns, completedDeals, promotedBloggers, promotedCampaigns);
        return new AdminDashboardDto(users.Result, bloggers.Result, businesses.Result, publishedCampaigns.Result, completedDeals.Result,
            promotedBloggers.Result, promotedCampaigns.Result);
    }
}
