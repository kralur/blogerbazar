using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CampaignApplicationReadModel(BloggerBazarDbContext dbContext) : ICampaignApplicationReadModel
{
    public async Task<MyCampaignApplicationsResult> SearchForBloggerAsync(Guid bloggerId, CampaignApplicationSearch search, CancellationToken cancellationToken)
    {
        var query = ApplyStatus(dbContext.CampaignApplications.AsNoTracking()
            .Where(application => application.BloggerId == bloggerId
                && !application.Campaign.Business.IsDeleted
                && application.Campaign.Business.ModerationStatus == BloggerStatus.Approved
                && !dbContext.PlatformUsers.Any(user => user.TelegramUserId == application.Campaign.Business.TelegramUserId && (user.IsBlocked || user.IsDeleted))), search.Status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(application => application.CreatedAtUtc).ThenByDescending(application => application.Id)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(application => new MyCampaignApplicationItemDto(
                application.Id,
                application.CampaignId,
                application.Campaign.Title,
                application.Campaign.Business.Name,
                application.Campaign.Business.LogoUrl,
                application.Campaign.City,
                application.Campaign.Categories,
                application.Campaign.BudgetFrom,
                application.Campaign.BudgetTo,
                application.Campaign.Deadline,
                application.Message,
                (int)application.Status,
                application.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new MyCampaignApplicationsResult(items, total, search.Page, search.PageSize, CampaignCatalogPagination.HasMore(total, search.Page, search.PageSize));
    }

    public Task<MyCampaignApplicationDetailsDto?> GetForBloggerAsync(Guid bloggerId, Guid applicationId, CancellationToken cancellationToken) =>
        dbContext.CampaignApplications.AsNoTracking()
            .Where(application => application.Id == applicationId
                && application.BloggerId == bloggerId
                && !application.Campaign.Business.IsDeleted
                && application.Campaign.Business.ModerationStatus == BloggerStatus.Approved
                && !dbContext.PlatformUsers.Any(user => user.TelegramUserId == application.Campaign.Business.TelegramUserId && (user.IsBlocked || user.IsDeleted)))
            .Select(application => new MyCampaignApplicationDetailsDto(
                application.Id,
                application.CampaignId,
                application.Campaign.Title,
                application.Campaign.Description,
                application.Campaign.Business.Name,
                application.Campaign.Business.LogoUrl,
                application.Campaign.City,
                application.Campaign.Categories,
                application.Campaign.Requirements,
                application.Campaign.BudgetFrom,
                application.Campaign.BudgetTo,
                application.Campaign.Deadline,
                application.Message,
                (int)application.Status,
                application.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CampaignApplicationInboxResult> SearchForBusinessAsync(Guid businessId, Guid campaignId, CampaignApplicationSearch search, CancellationToken cancellationToken)
    {
        var query = ApplyStatus(dbContext.CampaignApplications.AsNoTracking()
            .Where(application => application.CampaignId == campaignId
                && application.Campaign.BusinessId == businessId
                && !application.Blogger.IsDeleted
                && application.Blogger.Status == BloggerStatus.Approved
                && !dbContext.PlatformUsers.Any(user => user.TelegramUserId == application.Blogger.TelegramUserId && (user.IsBlocked || user.IsDeleted))), search.Status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(application => application.CreatedAtUtc).ThenByDescending(application => application.Id)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(application => new CampaignApplicationInboxItemDto(
                application.Id,
                application.BloggerId,
                application.Blogger.Name,
                application.Blogger.AvatarUrl,
                application.Blogger.City,
                application.Blogger.Categories,
                application.Message,
                (int)application.Status,
                application.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new CampaignApplicationInboxResult(items, total, search.Page, search.PageSize, CampaignCatalogPagination.HasMore(total, search.Page, search.PageSize));
    }

    private static IQueryable<CampaignApplication> ApplyStatus(IQueryable<CampaignApplication> query, int? status) =>
        status.HasValue ? query.Where(application => application.Status == (CampaignApplicationStatus)status.Value) : query;
}
