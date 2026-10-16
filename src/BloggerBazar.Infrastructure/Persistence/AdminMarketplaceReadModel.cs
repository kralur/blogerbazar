using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Application.Features.Complaints;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class AdminMarketplaceReadModel(BloggerBazarDbContext dbContext) : IAdminMarketplaceReadModel
{
    public async Task<IReadOnlyList<AdminBloggerProfileDto>> GetBloggersAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BloggerProfiles.AsNoTracking().OrderByDescending(profile => profile.CreatedAtUtc).Take(take)
            .Select(profile => new AdminBloggerProfileDto(profile.Id, profile.Name, profile.City, profile.Categories,
                profile.AvatarUrl, profile.TotalFollowers, (int)profile.Status, profile.CreatedAtUtc, profile.TelegramUserId))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminCampaignDto>> GetCampaignsAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.Campaigns.AsNoTracking().OrderByDescending(campaign => campaign.CreatedAtUtc).Take(take)
            .Select(campaign => new AdminCampaignDto(campaign.Id, campaign.Title, campaign.Business.Name, (int)campaign.Status,
                campaign.IsPromoted, campaign.Applications.Count, campaign.CreatedAtUtc, campaign.Business.TelegramUserId))
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
                dbContext.BrandFaceProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted),
                user.LastSeenAtUtc, user.Source))
            .ToArrayAsync(cancellationToken);
    }

    // Sequential queries: one DbContext must not run two at once.
    public async Task<AdminUserDetailsDto?> GetUserDetailsAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var user = await SearchUsersByIdAsync(telegramUserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var blogger = await dbContext.BloggerProfiles.AsNoTracking().Where(profile => profile.TelegramUserId == telegramUserId)
            .Select(profile => new AdminUserProfileDto("blogger", profile.Id, profile.Name, profile.IsHidden, profile.IsDeleted)).FirstOrDefaultAsync(cancellationToken);
        var brandFace = await dbContext.BrandFaceProfiles.AsNoTracking().Where(profile => profile.TelegramUserId == telegramUserId)
            .Select(profile => new AdminUserProfileDto("brandFace", profile.Id, profile.Name, profile.IsHidden, profile.IsDeleted)).FirstOrDefaultAsync(cancellationToken);
        var business = await dbContext.BusinessProfiles.AsNoTracking().Where(profile => profile.TelegramUserId == telegramUserId)
            .Select(profile => new AdminUserProfileDto("business", profile.Id, profile.Name, profile.IsHidden, profile.IsDeleted)).FirstOrDefaultAsync(cancellationToken);
        var profiles = new[] { blogger, brandFace, business }.OfType<AdminUserProfileDto>().ToArray();

        Guid? bloggerId = blogger?.Id, brandFaceId = brandFace?.Id, businessId = business?.Id;
        var deals = dbContext.Deals.AsNoTracking()
            .Where(deal => (bloggerId != null && deal.BloggerId == bloggerId) || (brandFaceId != null && deal.BrandFaceId == brandFaceId) || (businessId != null && deal.BusinessId == businessId));
        var counts = await deals.GroupBy(deal => deal.Status).Select(group => new { Status = group.Key, Count = group.Count() }).ToArrayAsync(cancellationToken);
        var recent = await deals.OrderByDescending(deal => deal.CreatedAtUtc).Take(10)
            .Select(deal => new AdminUserDealDto(
                deal.Id,
                businessId != null && deal.BusinessId == businessId ? "business" : deal.BloggerId != null ? "blogger" : "brandFace",
                businessId != null && deal.BusinessId == businessId
                    ? (deal.BloggerId != null ? deal.Blogger!.Name : deal.BrandFace!.Name)
                    : deal.Business.Name,
                deal.CampaignTitleSnapshot,
                (int)deal.Status,
                deal.AgreedPrice,
                deal.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        // Complaints against the person: any of their profiles, or a campaign of their business.
        var profileIds = profiles.Select(profile => profile.Id).ToArray();
        var against = dbContext.Complaints.AsNoTracking().Where(complaint => profileIds.Contains(complaint.TargetId)
            || (businessId != null && complaint.TargetType == ComplaintTargets.Campaign && dbContext.Campaigns.Any(campaign => campaign.Id == complaint.TargetId && campaign.BusinessId == businessId)));
        var complaintsAgainst = await against.CountAsync(cancellationToken);
        var openComplaintsAgainst = await against.CountAsync(complaint => complaint.Status == Complaint.OpenStatus, cancellationToken);
        var complaintsFiled = await dbContext.Complaints.CountAsync(complaint => complaint.ReporterTelegramUserId == telegramUserId, cancellationToken);

        int Count(DealStatus status) => counts.FirstOrDefault(item => item.Status == status)?.Count ?? 0;
        return new AdminUserDetailsDto(user, profiles, Count(DealStatus.Active), Count(DealStatus.Completed), Count(DealStatus.Cancelled),
            recent, complaintsAgainst, openComplaintsAgainst, complaintsFiled);
    }

    private async Task<AdminPlatformUserDto?> SearchUsersByIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers.AsNoTracking()
            .Where(user => user.TelegramUserId == telegramUserId)
            .Select(user => new AdminPlatformUserDto(user.TelegramUserId, user.FirstName, user.Username, user.Role, user.IsBlocked, user.IsDeleted,
                user.DeletedAtUtc, user.DeletedByTelegramUserId, user.CreatedAtUtc, user.VerifiedPhone,
                dbContext.BloggerProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted),
                dbContext.BusinessProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted),
                dbContext.BrandFaceProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId && !profile.IsDeleted),
                user.LastSeenAtUtc, user.Source))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminBusinessDto>> GetBusinessesAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BusinessProfiles.AsNoTracking().OrderByDescending(profile => profile.CreatedAtUtc).Take(take)
            .Select(profile => new AdminBusinessDto(profile.Id, profile.Name, profile.City, profile.LogoUrl, (int)profile.ModerationStatus,
                profile.IsHidden, profile.IsDeleted, profile.Campaigns.Count, profile.Deals.Count(deal => deal.Status == DealStatus.Completed), profile.CreatedAtUtc,
                profile.TelegramUserId))
            .ToArrayAsync(cancellationToken);

    // A deal has a blogger or a brand face on the creator side (never both); deleted profiles stay for their deals (D43).
    public async Task<IReadOnlyList<AdminDealDto>> GetDealsAsync(DealStatus? status, int take, CancellationToken cancellationToken) =>
        await dbContext.Deals.AsNoTracking()
            .Where(deal => status == null || deal.Status == status)
            .OrderByDescending(deal => deal.CreatedAtUtc).Take(take)
            .Select(deal => new AdminDealDto(
                deal.Id,
                // Older campaign deals have no snapshot: the campaign's current title then.
                deal.CampaignTitleSnapshot ?? (deal.CampaignApplicationId != null ? deal.CampaignApplication!.Campaign.Title : null),
                deal.CollaborationRequestId != null,
                deal.Business.Name,
                deal.Business.TelegramUserId,
                deal.BloggerId != null ? "blogger" : "brandFace",
                deal.BloggerId != null ? deal.Blogger!.Name : deal.BrandFace!.Name,
                deal.BloggerId != null ? deal.Blogger!.TelegramUserId : deal.BrandFace!.TelegramUserId,
                (int)deal.Status,
                deal.AgreedPrice,
                deal.CreatedAtUtc,
                deal.CompletedAtUtc))
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
        // Profiles of deleted accounts are kept for their deals (D43) but are no longer bloggers or companies here,
        // the same way deleted and blocked people are not counted as users.
        var bloggers = await dbContext.BloggerProfiles.AsNoTracking().CountAsync(profile => !profile.IsDeleted, cancellationToken);
        var businesses = await dbContext.BusinessProfiles.AsNoTracking().CountAsync(profile => !profile.IsDeleted, cancellationToken);
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
        var activeToday = await dbContext.PlatformUsers.AsNoTracking().CountAsync(user => user.LastSeenAtUtc >= today, cancellationToken);
        var activeWeek = await dbContext.PlatformUsers.AsNoTracking().CountAsync(user => user.LastSeenAtUtc >= weekAgo, cancellationToken);
        var sources = (await dbContext.PlatformUsers.AsNoTracking()
            .Where(user => user.Source != null)
            .GroupBy(user => user.Source!)
            .Select(group => new { Source = group.Key, Users = group.Count() })
            .OrderByDescending(source => source.Users).ThenBy(source => source.Source)
            .Take(10)
            .ToArrayAsync(cancellationToken))
            .Select(source => new AdminSourceDto(source.Source, source.Users))
            .ToArray();
        return new AdminDashboardDto(users, bloggers, businesses, publishedCampaigns, completedDeals, promotedBloggers, promotedCampaigns, openComplaints,
            newUsersToday, newUsersWeek, newDealsWeek, completedDealsWeek, activeToday, activeWeek, sources);
    }

    // The cohort is everyone whose first app open (PlatformUser.CreatedAtUtc) is in the period, deleted and blocked people
    // included: they did reach their steps. Each later step is counted on the role's profiles of that cohort and also
    // accepts the steps after it (a deal counts as "reached"), so the numbers never grow down the funnel.
    // Sequential counts: one DbContext runs one query at a time.
    public async Task<IReadOnlyList<AdminFunnelRoleDto>> GetFunnelAsync(DateTime? fromUtc, CancellationToken cancellationToken)
    {
        var cohort = dbContext.PlatformUsers.AsNoTracking();
        if (fromUtc is { } from)
        {
            cohort = cohort.Where(user => user.CreatedAtUtc >= from);
        }

        var opened = await cohort.CountAsync(cancellationToken);
        var deals = dbContext.Deals.AsNoTracking();
        var completedDeals = deals.Where(deal => deal.Status == DealStatus.Completed);
        // A review still hidden until the other side reviews (blind reviews) was left all the same.
        var reviews = dbContext.Reviews.IgnoreQueryFilters().AsNoTracking().Where(review => review.Deal.Status == DealStatus.Completed);

        var bloggers = dbContext.BloggerProfiles.AsNoTracking().Where(profile => cohort.Any(user => user.TelegramUserId == profile.TelegramUserId));
        var blogger = new[]
        {
            new AdminFunnelStepDto(AdminFunnel.Opened, opened),
            new AdminFunnelStepDto(AdminFunnel.RoleSelected, await cohort.CountAsync(user => user.SelectedMarketplaceRole == MarketplaceRole.Blogger
                || dbContext.BloggerProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Profile, await bloggers.CountAsync(cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Reached, await bloggers.CountAsync(profile => dbContext.CampaignApplications.Any(application => application.BloggerId == profile.Id)
                || dbContext.CollaborationRequests.Any(offer => offer.BloggerId == profile.Id)
                || deals.Any(deal => deal.BloggerId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Deal, await bloggers.CountAsync(profile => deals.Any(deal => deal.BloggerId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Completed, await bloggers.CountAsync(profile => completedDeals.Any(deal => deal.BloggerId == profile.Id), cancellationToken)),
            // A review counts for the role whose deal it was written on.
            new AdminFunnelStepDto(AdminFunnel.Reviewed, await bloggers.CountAsync(profile => reviews.Any(review => review.ReviewerTelegramUserId == profile.TelegramUserId
                && review.Deal.BloggerId == profile.Id), cancellationToken))
        };

        var brandFaces = dbContext.BrandFaceProfiles.AsNoTracking().Where(profile => cohort.Any(user => user.TelegramUserId == profile.TelegramUserId));
        var brandFace = new[]
        {
            new AdminFunnelStepDto(AdminFunnel.Opened, opened),
            new AdminFunnelStepDto(AdminFunnel.RoleSelected, await cohort.CountAsync(user => user.SelectedMarketplaceRole == MarketplaceRole.BrandFace
                || dbContext.BrandFaceProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Profile, await brandFaces.CountAsync(cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Reached, await brandFaces.CountAsync(profile => dbContext.CampaignApplications.Any(application => application.BrandFaceId == profile.Id)
                || dbContext.CollaborationRequests.Any(offer => offer.BrandFaceId == profile.Id)
                || deals.Any(deal => deal.BrandFaceId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Deal, await brandFaces.CountAsync(profile => deals.Any(deal => deal.BrandFaceId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Completed, await brandFaces.CountAsync(profile => completedDeals.Any(deal => deal.BrandFaceId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Reviewed, await brandFaces.CountAsync(profile => reviews.Any(review => review.ReviewerTelegramUserId == profile.TelegramUserId
                && review.Deal.BrandFaceId == profile.Id), cancellationToken))
        };

        var businesses = dbContext.BusinessProfiles.AsNoTracking().Where(profile => cohort.Any(user => user.TelegramUserId == profile.TelegramUserId));
        var business = new[]
        {
            new AdminFunnelStepDto(AdminFunnel.Opened, opened),
            new AdminFunnelStepDto(AdminFunnel.RoleSelected, await cohort.CountAsync(user => user.SelectedMarketplaceRole == MarketplaceRole.Business
                || dbContext.BusinessProfiles.Any(profile => profile.TelegramUserId == user.TelegramUserId), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Profile, await businesses.CountAsync(cancellationToken)),
            // A draft is not seen by anyone, so only a campaign that went out counts (closed or rejected later included,
            // and one an admin moved back to draft after it got applications).
            new AdminFunnelStepDto(AdminFunnel.Posted, await businesses.CountAsync(profile => dbContext.Campaigns.Any(campaign => campaign.BusinessId == profile.Id
                    && (campaign.Status != CampaignStatus.Draft || campaign.Applications.Any()))
                || dbContext.CollaborationRequests.Any(offer => offer.BusinessId == profile.Id)
                || deals.Any(deal => deal.BusinessId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Answered, await businesses.CountAsync(profile => dbContext.CampaignApplications.Any(application => application.Campaign.BusinessId == profile.Id)
                || deals.Any(deal => deal.BusinessId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Deal, await businesses.CountAsync(profile => deals.Any(deal => deal.BusinessId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Completed, await businesses.CountAsync(profile => completedDeals.Any(deal => deal.BusinessId == profile.Id), cancellationToken)),
            new AdminFunnelStepDto(AdminFunnel.Reviewed, await businesses.CountAsync(profile => reviews.Any(review => review.ReviewerTelegramUserId == profile.TelegramUserId
                && review.Deal.BusinessId == profile.Id), cancellationToken))
        };

        return [new AdminFunnelRoleDto("blogger", blogger), new AdminFunnelRoleDto("brandFace", brandFace), new AdminFunnelRoleDto("business", business)];
    }
}
