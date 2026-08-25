using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CampaignManagementReadModel(BloggerBazarDbContext dbContext) : ICampaignManagementReadModel
{
    public async Task<MyCampaignsResult> SearchAsync(Guid businessId, MyCampaignsSearch search, CancellationToken cancellationToken)
    {
        var query = CampaignManagementFiltering.Apply(CampaignManagementOwnership.ForBusiness(dbContext.Campaigns.AsNoTracking(), businessId), search);
        var total = await query.CountAsync(cancellationToken);
        var items = await CampaignManagementProjection.Items(CampaignManagementSorting.Apply(query, search.Sort)
                .Skip((search.Page - 1) * search.PageSize)
                .Take(search.PageSize))
            .ToArrayAsync(cancellationToken);

        return new MyCampaignsResult(items, total, search.Page, search.PageSize, CampaignCatalogPagination.HasMore(total, search.Page, search.PageSize));
    }

    public Task<MyCampaignDetailsDto?> GetByIdAsync(Guid businessId, Guid campaignId, CancellationToken cancellationToken) =>
        CampaignManagementProjection.Details(CampaignManagementOwnership.ForBusiness(dbContext.Campaigns.AsNoTracking(), businessId)
                .Where(campaign => campaign.Id == campaignId))
            .SingleOrDefaultAsync(cancellationToken);
}

internal static class CampaignManagementOwnership
{
    internal static IQueryable<Campaign> ForBusiness(IQueryable<Campaign> query, Guid businessId) =>
        query.Where(campaign => campaign.BusinessId == businessId);
}

internal static class CampaignManagementFiltering
{
    internal static IQueryable<Campaign> Apply(IQueryable<Campaign> query, MyCampaignsSearch search)
    {
        if (search.Status.HasValue)
        {
            var status = (CampaignStatus)search.Status.Value;
            query = query.Where(campaign => campaign.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var pattern = PostgresSearchPattern.Contains(search.Query);
            query = query.Where(campaign => EF.Functions.ILike(campaign.Title, pattern)
                || EF.Functions.ILike(campaign.Description, pattern)
                || (campaign.City != null && EF.Functions.ILike(campaign.City, pattern))
                || campaign.Categories.Any(category => EF.Functions.ILike(category, pattern)));
        }

        return query;
    }
}

internal static class CampaignManagementSorting
{
    internal static IOrderedQueryable<Campaign> Apply(IQueryable<Campaign> query, string sort) =>
        sort switch
        {
            "newest" => query.OrderByDescending(campaign => campaign.CreatedAtUtc).ThenBy(campaign => campaign.Id),
            "oldest" => query.OrderBy(campaign => campaign.CreatedAtUtc).ThenBy(campaign => campaign.Id),
            "deadline_asc" => query.OrderBy(campaign => campaign.Deadline == null).ThenBy(campaign => campaign.Deadline).ThenBy(campaign => campaign.Id),
            "deadline_desc" => query.OrderBy(campaign => campaign.Deadline == null).ThenByDescending(campaign => campaign.Deadline).ThenBy(campaign => campaign.Id),
            "budget_asc" => query.OrderBy(campaign => campaign.BudgetFrom == null && campaign.BudgetTo == null).ThenBy(campaign => campaign.BudgetFrom ?? campaign.BudgetTo).ThenBy(campaign => campaign.Id),
            "budget_desc" => query.OrderBy(campaign => campaign.BudgetFrom == null && campaign.BudgetTo == null).ThenByDescending(campaign => campaign.BudgetTo ?? campaign.BudgetFrom).ThenBy(campaign => campaign.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "The campaign sort is invalid.")
        };
}

internal static class CampaignManagementProjection
{
    internal static IQueryable<MyCampaignItemDto> Items(IQueryable<Campaign> query) =>
        query.Select(campaign => new MyCampaignItemDto(
            campaign.Id,
            campaign.Title,
            campaign.City,
            campaign.Categories,
            campaign.BudgetFrom,
            campaign.BudgetTo,
            campaign.Deadline,
            (int)campaign.Status,
            campaign.IsPromoted,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            campaign.Applications.Count));

    internal static IQueryable<MyCampaignDetailsDto> Details(IQueryable<Campaign> query) =>
        query.Select(campaign => new MyCampaignDetailsDto(
            campaign.Id,
            campaign.Title,
            campaign.Description,
            campaign.City,
            campaign.Categories,
            campaign.Requirements,
            campaign.BudgetFrom,
            campaign.BudgetTo,
            campaign.Deadline,
            (int)campaign.Status,
            campaign.IsPromoted,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            campaign.Applications.Count));
}
