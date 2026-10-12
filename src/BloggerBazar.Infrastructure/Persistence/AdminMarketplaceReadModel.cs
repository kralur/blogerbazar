using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Domain.Entities;
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

    public async Task<IReadOnlyList<AdminPlatformUserDto>> SearchUsersAsync(string? search, string? filter, int take, CancellationToken cancellationToken)
    {
        var users = dbContext.PlatformUsers.AsNoTracking();
        users = filter switch
        {
            AdminUserFilters.Bloggers => users.Where(user => dbContext.BloggerProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted)),
            AdminUserFilters.Businesses => users.Where(user => dbContext.BusinessProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted)),
            AdminUserFilters.BrandFaces => users.Where(user => dbContext.BrandFaceProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted)),
            AdminUserFilters.Admins => users.Where(user => user.Role == PlatformRole.Owner || user.Role == PlatformRole.Admin),
            AdminUserFilters.Blocked => users.Where(user => user.IsBlocked),
            AdminUserFilters.Deleted => users.Where(user => user.IsDeleted),
            // Same window as the "new users in 7 days" counter on the admin home.
            AdminUserFilters.New => users.Where(user => user.CreatedAtUtc >= DateTime.UtcNow.AddDays(-AdminUserFilters.NewDays)),
            _ => users
        };

        if (search is not null)
        {
            var pattern = PostgresSearchPattern.Contains(search.TrimStart('@'));
            // A phone is searched by its digits, so "90 123 45 67" finds "+998901234567".
            var digits = new string(search.Where(char.IsDigit).ToArray());
            var digitsPattern = digits.Length >= 3 ? PostgresSearchPattern.Contains(digits) : null;
            users = users.Where(user => EF.Functions.ILike(user.FirstName, pattern)
                || (user.Username != null && EF.Functions.ILike(user.Username, pattern))
                || (digitsPattern != null && user.VerifiedPhone != null && EF.Functions.Like(user.VerifiedPhone, digitsPattern))
                || (digitsPattern != null && EF.Functions.Like(user.TelegramUserId.ToString(), digitsPattern)));
        }

        return await users
            .OrderByDescending(user => user.CreatedAtUtc)
            .Take(take)
            .Select(user => new AdminPlatformUserDto(user.TelegramUserId, user.FirstName, user.Username, user.Role, user.IsBlocked, user.IsDeleted,
                user.DeletedAtUtc, user.DeletedByTelegramUserId, user.CreatedAtUtc, user.VerifiedPhone,
                dbContext.BloggerProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted),
                dbContext.BusinessProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted),
                dbContext.BrandFaceProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted)))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminBusinessDto>> GetBusinessesAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BusinessProfiles.AsNoTracking().OrderByDescending(profile => profile.CreatedAtUtc).Take(take)
            .Select(profile => new AdminBusinessDto(profile.Id, profile.Name, profile.City, profile.LogoUrl, (int)profile.ModerationStatus,
                profile.IsHidden, profile.IsDeleted, profile.Campaigns.Count, profile.Deals.Count(deal => deal.Status == DealStatus.Completed), profile.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequestsAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.CollaborationRequests.AsNoTracking().OrderByDescending(request => request.CreatedAtUtc).Take(take)
            .Select(request => new CollaborationRequestDto(request.Id, request.BloggerId, request.BloggerId != null ? request.Blogger!.Name : request.BrandFace!.Name, request.BusinessId,
                request.Business.Name, request.Message, (int)request.Status, request.Deal == null ? null : request.Deal.Id, request.CreatedAtUtc, request.BrandFaceId))
            .ToArrayAsync(cancellationToken);

    // One DbContext runs one query at a time: parallel CountAsync calls on it throw, so the counts go in sequence.
    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.PlatformUsers.AsNoTracking().CountAsync(user => !user.IsBlocked && !user.IsDeleted, cancellationToken);
        var bloggers = await dbContext.BloggerProfiles.AsNoTracking().CountAsync(cancellationToken);
        var businesses = await dbContext.BusinessProfiles.AsNoTracking().CountAsync(cancellationToken);
        var publishedCampaigns = await dbContext.Campaigns.AsNoTracking().CountAsync(campaign => campaign.Status == CampaignStatus.Published, cancellationToken);
        var completedDeals = await dbContext.Deals.AsNoTracking().CountAsync(deal => deal.Status == DealStatus.Completed, cancellationToken);
        var promotedBloggers = await dbContext.BloggerProfiles.AsNoTracking().CountAsync(profile => profile.IsPromoted, cancellationToken);
        var promotedCampaigns = await dbContext.Campaigns.AsNoTracking().CountAsync(campaign => campaign.IsPromoted, cancellationToken);
        var openComplaints = await dbContext.Complaints.AsNoTracking().CountAsync(complaint => complaint.Status == Complaint.OpenStatus, cancellationToken);
        // Activity for the owner: today is the UTC day, the week is the last 7 days.
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = now.AddDays(-7);
        var newUsersToday = await dbContext.PlatformUsers.AsNoTracking().CountAsync(user => user.CreatedAtUtc >= today, cancellationToken);
        var newUsersWeek = await dbContext.PlatformUsers.AsNoTracking().CountAsync(user => user.CreatedAtUtc >= weekAgo, cancellationToken);
        var newDealsWeek = await dbContext.Deals.AsNoTracking().CountAsync(deal => deal.CreatedAtUtc >= weekAgo, cancellationToken);
        var completedDealsWeek = await dbContext.Deals.AsNoTracking().CountAsync(deal => deal.CompletedAtUtc >= weekAgo, cancellationToken);
        return new AdminDashboardDto(users, bloggers, businesses, publishedCampaigns, completedDeals, promotedBloggers, promotedCampaigns, openComplaints,
            newUsersToday, newUsersWeek, newDealsWeek, completedDealsWeek);
    }
}
