using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Application.Features.CollaborationRequests;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class MarketplaceCatalogReadModel(BloggerBazarDbContext dbContext) : IMarketplaceCatalogReadModel
{
    public async Task<SearchBloggersResult> SearchBloggersAsync(BloggerCatalogSearch search, CancellationToken cancellationToken)
    {
        var query = dbContext.BloggerProfiles.AsNoTracking().Where(profile => !profile.IsDeleted && profile.Status == BloggerStatus.Approved);
        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var pattern = PostgresSearchPattern.Contains(search.Query.Trim());
            query = query.Where(profile => EF.Functions.ILike(profile.Name, pattern)
                || EF.Functions.ILike(profile.City, pattern)
                || profile.Categories.Any(category => EF.Functions.ILike(category, pattern)));
        }
        if (!string.IsNullOrWhiteSpace(search.City))
        {
            var city = search.City.Trim();
            query = query.Where(profile => profile.City == city);
        }
        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            var category = search.Category.Trim();
            query = query.Where(profile => profile.Categories.Contains(category));
        }
        if (!string.IsNullOrWhiteSpace(search.Platform))
        {
            var platform = search.Platform.Trim();
            query = query.Where(profile => profile.Platforms.Any(item => item.Type == platform));
        }
        if (search.MinFollowers.HasValue) query = query.Where(profile => profile.TotalFollowers >= search.MinFollowers.Value);
        if (search.MinEngagementRate.HasValue) query = query.Where(profile => profile.EngagementRate >= search.MinEngagementRate.Value);
        if (search.MaxEngagementRate.HasValue) query = query.Where(profile => profile.EngagementRate <= search.MaxEngagementRate.Value);
        if (search.MinPrice.HasValue)
        {
            query = query.Where(profile => (profile.StoriesPrice != null && profile.StoriesPrice >= search.MinPrice)
                || (profile.ReelsPrice != null && profile.ReelsPrice >= search.MinPrice)
                || (profile.PostPrice != null && profile.PostPrice >= search.MinPrice)
                || (profile.IntegrationPrice != null && profile.IntegrationPrice >= search.MinPrice));
        }
        if (search.MaxPrice.HasValue)
        {
            query = query.Where(profile => (profile.StoriesPrice != null && profile.StoriesPrice <= search.MaxPrice)
                || (profile.ReelsPrice != null && profile.ReelsPrice <= search.MaxPrice)
                || (profile.PostPrice != null && profile.PostPrice <= search.MaxPrice)
                || (profile.IntegrationPrice != null && profile.IntegrationPrice <= search.MaxPrice));
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = search.Sort switch
        {
            "rating" => query.OrderByDescending(profile => profile.Reviews.Average(review => (decimal?)review.Rating) ?? 0).ThenByDescending(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed)).ThenByDescending(profile => profile.Reviews.Count),
            "er" => query.OrderByDescending(profile => profile.EngagementRate ?? 0).ThenByDescending(profile => profile.TotalFollowers),
            "price" => query.OrderBy(profile => profile.StoriesPrice ?? profile.ReelsPrice ?? profile.PostPrice ?? profile.IntegrationPrice ?? int.MaxValue).ThenByDescending(profile => profile.TotalFollowers),
            "newest" => query.OrderByDescending(profile => profile.CreatedAtUtc),
            _ => query.OrderByDescending(profile => profile.Deals.Count(deal => deal.Status == DealStatus.Completed)).ThenByDescending(profile => profile.Reviews.Average(review => (decimal?)review.Rating) ?? 0).ThenByDescending(profile => profile.TotalFollowers)
        };

        var bloggers = await ProjectBloggersAsync(ordered.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize), cancellationToken);
        return new SearchBloggersResult(bloggers, total, search.Page, search.PageSize);
    }

    public async Task<BloggerProfileDto?> GetBloggerAsync(Guid id, CancellationToken cancellationToken) =>
        (await ProjectBloggersAsync(dbContext.BloggerProfiles.AsNoTracking().Where(profile => profile.Id == id && !profile.IsDeleted), cancellationToken)).SingleOrDefault();

    public async Task<MyBloggerProfileDto?> GetMyBloggerAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.BloggerProfiles.AsNoTracking()
            .Where(item => item.TelegramUserId == telegramUserId && !item.IsDeleted)
            .Select(item => new MyBloggerRow(item.Id, item.Name, item.LastName, item.Username, item.City, item.Categories, item.Bio,
                item.AvatarUrl, item.Phone, item.Email, item.TotalFollowers, item.AverageReach, item.EngagementRate, item.StoriesPrice,
                item.ReelsPrice, item.PostPrice, item.IntegrationPrice, item.BarterEnabled, (int)item.Status))
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null) return null;

        var portfolioItems = await dbContext.PortfolioItems.AsNoTracking().Where(item => item.BloggerId == profile.Id)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new PortfolioItemDto(item.Id, item.Title, (int)item.Type, item.Url)).ToArrayAsync(cancellationToken);
        var platforms = await dbContext.SocialPlatforms.AsNoTracking().Where(item => item.BloggerId == profile.Id)
            .Select(item => new SocialPlatformDto(item.Id, item.Type, item.Url, item.Followers, item.ScreenshotUrl)).ToArrayAsync(cancellationToken);
        return new MyBloggerProfileDto(profile.Id, profile.Name, profile.LastName, profile.Username, profile.City, profile.Categories,
            profile.Bio, profile.AvatarUrl, profile.Phone, profile.Email, profile.TotalFollowers, profile.AverageReach, profile.EngagementRate,
            profile.StoriesPrice, profile.ReelsPrice, profile.PostPrice, profile.IntegrationPrice, profile.BarterEnabled, profile.Status, portfolioItems, platforms);
    }

    public async Task<IReadOnlyList<CampaignDto>> SearchCampaignsAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.Campaigns.AsNoTracking().Where(campaign => campaign.Status == CampaignStatus.Published && !campaign.Business.IsDeleted);
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(campaign => campaign.City == city.Trim());
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(campaign => campaign.Categories.Contains(category.Trim()));
        return await ProjectCampaigns(query.OrderByDescending(campaign => campaign.IsPromoted).ThenByDescending(campaign => campaign.CreatedAtUtc).Skip(skip).Take(take))
            .ToArrayAsync(cancellationToken);
    }

    public Task<CampaignDto?> GetCampaignAsync(Guid id, CancellationToken cancellationToken) =>
        ProjectCampaigns(dbContext.Campaigns.AsNoTracking().Where(campaign => campaign.Id == id && campaign.Status == CampaignStatus.Published && !campaign.Business.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MyCampaignApplicationDto>> GetCampaignApplicationsAsync(Guid? bloggerId, Guid? businessId, CancellationToken cancellationToken)
    {
        var response = new List<MyCampaignApplicationDto>();
        if (bloggerId.HasValue)
        {
            response.AddRange(await dbContext.CampaignApplications.AsNoTracking().Where(application => application.BloggerId == bloggerId && !application.Campaign.Business.IsDeleted)
                .Select(application => new MyCampaignApplicationDto(application.Id, application.CampaignId, application.Campaign.Title,
                    application.Campaign.Business.Name, application.Campaign.Business.LogoUrl, application.Message, (int)application.Status, false, application.CreatedAtUtc))
                .ToArrayAsync(cancellationToken));
        }
        if (businessId.HasValue)
        {
            response.AddRange(await dbContext.CampaignApplications.AsNoTracking().Where(application => application.Campaign.BusinessId == businessId && !application.Blogger.IsDeleted)
                .Select(application => new MyCampaignApplicationDto(application.Id, application.CampaignId, application.Campaign.Title,
                    application.Blogger.Name, application.Blogger.AvatarUrl, application.Message, (int)application.Status, true, application.CreatedAtUtc))
                .ToArrayAsync(cancellationToken));
        }
        return response.OrderByDescending(application => application.CreatedAtUtc).ToArray();
    }

    public async Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequestsAsync(Guid? bloggerId, Guid? businessId, int take, CancellationToken cancellationToken)
    {
        var response = new List<CollaborationRequestDto>();
        if (bloggerId.HasValue)
        {
            response.AddRange(await ProjectCollaborationRequests(dbContext.CollaborationRequests.AsNoTracking().Where(request => request.BloggerId == bloggerId && !request.Business.IsDeleted).Take(take))
                .ToArrayAsync(cancellationToken));
        }
        if (businessId.HasValue)
        {
            response.AddRange(await ProjectCollaborationRequests(dbContext.CollaborationRequests.AsNoTracking().Where(request => request.BusinessId == businessId && !request.Blogger.IsDeleted).Take(take))
                .ToArrayAsync(cancellationToken));
        }
        return response.OrderByDescending(request => request.CreatedAtUtc).ToArray();
    }

    public async Task<IReadOnlyList<MyDealDto>> GetDealsAsync(Guid? bloggerId, Guid? businessId, long telegramUserId, CancellationToken cancellationToken)
    {
        if (bloggerId is null && businessId is null) return [];
        var query = dbContext.Deals.AsNoTracking().Where(deal => !deal.Blogger.IsDeleted && !deal.Business.IsDeleted);
        if (bloggerId is not null && businessId is not null) query = query.Where(deal => deal.BloggerId == bloggerId || deal.BusinessId == businessId);
        else if (bloggerId is not null) query = query.Where(deal => deal.BloggerId == bloggerId);
        else query = query.Where(deal => deal.BusinessId == businessId);

        return await query.OrderByDescending(deal => deal.CreatedAtUtc)
            .Select(deal => new MyDealDto(deal.Id, deal.CampaignApplicationId, deal.CollaborationRequestId,
                deal.CampaignApplication == null ? "Direct collaboration request" : deal.CampaignApplication.Campaign.Title,
                deal.Blogger.TelegramUserId == telegramUserId ? deal.Business.Name : deal.Blogger.Name,
                deal.Blogger.TelegramUserId == telegramUserId ? deal.Business.LogoUrl : deal.Blogger.AvatarUrl,
                (int)deal.Status, deal.CreatedAtUtc, deal.CompletedAtUtc, deal.Status == DealStatus.Active,
                deal.Status == DealStatus.Completed && !deal.Reviews.Any(review => review.ReviewerTelegramUserId == telegramUserId)))
            .ToArrayAsync(cancellationToken);
    }

    private IQueryable<CampaignDto> ProjectCampaigns(IQueryable<Campaign> query) =>
        query.Select(campaign => new CampaignDto(campaign.Id, campaign.BusinessId, campaign.Business.Name, campaign.Title,
            campaign.Description, campaign.City, campaign.Categories, campaign.Requirements, campaign.BudgetFrom, campaign.BudgetTo,
            campaign.Deadline, campaign.IsPromoted, (int)campaign.Status, campaign.Applications.Count(application => !application.Blogger.IsDeleted), campaign.CreatedAtUtc));

    private IQueryable<CollaborationRequestDto> ProjectCollaborationRequests(IQueryable<CollaborationRequest> query) =>
        query.OrderByDescending(request => request.CreatedAtUtc).Select(request => new CollaborationRequestDto(request.Id, request.BloggerId,
            request.Blogger.Name, request.BusinessId, request.Business.Name, request.Message, (int)request.Status,
            request.Deal == null ? null : request.Deal.Id, request.CreatedAtUtc));

    private async Task<IReadOnlyList<BloggerProfileDto>> ProjectBloggersAsync(IQueryable<BloggerProfile> query, CancellationToken cancellationToken)
    {
        var profiles = await query.Select(profile => new BloggerRow(profile.Id, profile.Name, profile.City, profile.Categories, profile.Bio,
                profile.AvatarUrl, profile.CoverUrl, profile.TotalFollowers, profile.AverageReach, profile.EngagementRate, profile.StoriesPrice,
                profile.ReelsPrice, profile.PostPrice, profile.IntegrationPrice, profile.BarterEnabled, profile.IsVerified, profile.IsPromoted,
                (int)profile.Status, profile.Reviews.Average(review => (decimal?)review.Rating), profile.Reviews.Count,
                profile.Deals.Count(deal => deal.Status == DealStatus.Completed)))
            .ToArrayAsync(cancellationToken);
        if (profiles.Length == 0) return [];

        var ids = profiles.Select(profile => profile.Id).ToArray();
        var portfolioItems = await dbContext.PortfolioItems.AsNoTracking().Where(item => ids.Contains(item.BloggerId))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => new PortfolioItemRow(item.BloggerId, new PortfolioItemDto(item.Id, item.Title, (int)item.Type, item.Url)))
            .ToArrayAsync(cancellationToken);
        var platforms = await dbContext.SocialPlatforms.AsNoTracking().Where(platform => ids.Contains(platform.BloggerId))
            .Select(platform => new SocialPlatformRow(platform.BloggerId, new SocialPlatformDto(platform.Id, platform.Type, platform.Url, platform.Followers, platform.ScreenshotUrl)))
            .ToArrayAsync(cancellationToken);

        var portfoliosByBlogger = portfolioItems.GroupBy(item => item.BloggerId).ToDictionary(group => group.Key, group => (IReadOnlyCollection<PortfolioItemDto>)group.Select(item => item.Item).ToArray());
        var platformsByBlogger = platforms.GroupBy(item => item.BloggerId).ToDictionary(group => group.Key, group => (IReadOnlyCollection<SocialPlatformDto>)group.Select(item => item.Platform).ToArray());
        return profiles.Select(profile => new BloggerProfileDto(profile.Id, profile.Name, profile.City, profile.Categories, profile.Bio,
            profile.AvatarUrl, profile.CoverUrl, profile.TotalFollowers, CreatorLevel(profile.TotalFollowers), profile.AverageReach,
            profile.EngagementRate, profile.StoriesPrice, profile.ReelsPrice, profile.PostPrice, profile.IntegrationPrice, profile.BarterEnabled,
            profile.IsVerified, profile.IsPromoted, profile.Status, profile.Rating, profile.ReviewsCount, profile.CompletedDealsCount,
            portfoliosByBlogger.GetValueOrDefault(profile.Id, []), platformsByBlogger.GetValueOrDefault(profile.Id, []))).ToArray();
    }

    private static int CreatorLevel(int totalFollowers) => totalFollowers switch { < 5_000 => 0, < 50_000 => 1, < 500_000 => 2, _ => 3 };

    private sealed record BloggerRow(Guid Id, string Name, string City, IReadOnlyCollection<string> Categories, string? Bio, string? AvatarUrl,
        string? CoverUrl, int TotalFollowers, int? AverageReach, decimal? EngagementRate, int? StoriesPrice, int? ReelsPrice,
        int? PostPrice, int? IntegrationPrice, bool BarterEnabled, bool IsVerified, bool IsPromoted, int Status, decimal? Rating,
        int ReviewsCount, int CompletedDealsCount);
    private sealed record MyBloggerRow(Guid Id, string Name, string? LastName, string? Username, string City, IReadOnlyCollection<string> Categories,
        string? Bio, string? AvatarUrl, string? Phone, string? Email, int TotalFollowers, int? AverageReach, decimal? EngagementRate,
        int? StoriesPrice, int? ReelsPrice, int? PostPrice, int? IntegrationPrice, bool BarterEnabled, int Status);
    private sealed record PortfolioItemRow(Guid BloggerId, PortfolioItemDto Item);
    private sealed record SocialPlatformRow(Guid BloggerId, SocialPlatformDto Platform);
}
