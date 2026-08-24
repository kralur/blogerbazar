using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Campaigns;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CampaignCatalogReadModel(BloggerBazarDbContext dbContext) : ICampaignCatalogReadModel
{
    public async Task<CampaignCatalogResult> SearchAsync(CampaignCatalogSearch search, CancellationToken cancellationToken)
    {
        var query = MarketplaceCatalogVisibility.PublicCampaigns(
            dbContext.Campaigns.AsNoTracking(),
            dbContext.BusinessProfiles.AsNoTracking(),
            dbContext.PlatformUsers.AsNoTracking());

        query = CampaignCatalogFiltering.Apply(query, search);

        var total = await query.CountAsync(cancellationToken);
        var items = await CampaignCatalogSorting.Apply(query, search.Sort)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(campaign => new CampaignCatalogItemDto(
                campaign.Id,
                campaign.Title,
                campaign.Business.Name,
                campaign.Business.LogoUrl,
                campaign.City,
                campaign.Categories,
                campaign.Requirements,
                campaign.BudgetFrom,
                campaign.BudgetTo,
                campaign.Deadline,
                (int)campaign.Status,
                campaign.IsPromoted,
                campaign.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new CampaignCatalogResult(items, total, search.Page, search.PageSize, CampaignCatalogPagination.HasMore(total, search.Page, search.PageSize));
    }
}

internal static class CampaignCatalogFiltering
{
    internal static IQueryable<Campaign> Apply(IQueryable<Campaign> query, CampaignCatalogSearch search)
    {
        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var pattern = PostgresSearchPattern.Contains(search.Query);
            query = query.Where(campaign => EF.Functions.ILike(campaign.Title, pattern)
                || EF.Functions.ILike(campaign.Description, pattern)
                || (campaign.City != null && EF.Functions.ILike(campaign.City, pattern))
                || campaign.Categories.Any(category => EF.Functions.ILike(category, pattern))
                || EF.Functions.ILike(campaign.Business.Name, pattern));
        }

        if (!string.IsNullOrWhiteSpace(search.City))
        {
            query = query.Where(campaign => campaign.City == search.City);
        }

        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            query = query.Where(campaign => campaign.Categories.Contains(search.Category));
        }

        if (search.MinBudget.HasValue)
        {
            query = query.Where(campaign => (campaign.BudgetFrom != null || campaign.BudgetTo != null)
                && (campaign.BudgetTo ?? campaign.BudgetFrom) >= search.MinBudget.Value);
        }

        if (search.MaxBudget.HasValue)
        {
            query = query.Where(campaign => (campaign.BudgetFrom != null || campaign.BudgetTo != null)
                && (campaign.BudgetFrom ?? campaign.BudgetTo) <= search.MaxBudget.Value);
        }

        if (search.DeadlineFrom.HasValue)
        {
            query = query.Where(campaign => campaign.Deadline != null && campaign.Deadline >= search.DeadlineFrom.Value);
        }

        if (search.DeadlineTo.HasValue)
        {
            query = query.Where(campaign => campaign.Deadline != null && campaign.Deadline <= search.DeadlineTo.Value);
        }

        return query;
    }
}

internal static class CampaignCatalogSorting
{
    internal static IOrderedQueryable<Campaign> Apply(IQueryable<Campaign> query, string sort) =>
        sort switch
        {
            "newest" => query.OrderByDescending(campaign => campaign.CreatedAtUtc).ThenBy(campaign => campaign.Id),
            "deadline_asc" => query.OrderBy(campaign => campaign.Deadline == null).ThenBy(campaign => campaign.Deadline).ThenBy(campaign => campaign.Id),
            "budget_asc" => query.OrderBy(campaign => campaign.BudgetFrom == null && campaign.BudgetTo == null).ThenBy(campaign => campaign.BudgetFrom ?? campaign.BudgetTo).ThenBy(campaign => campaign.Id),
            "budget_desc" => query.OrderBy(campaign => campaign.BudgetFrom == null && campaign.BudgetTo == null).ThenByDescending(campaign => campaign.BudgetTo ?? campaign.BudgetFrom).ThenBy(campaign => campaign.Id),
            _ => query.OrderByDescending(campaign => campaign.IsPromoted).ThenByDescending(campaign => campaign.CreatedAtUtc).ThenBy(campaign => campaign.Id)
        };
}

internal static class CampaignCatalogPagination
{
    internal static bool HasMore(int total, int page, int pageSize) => total > (long)page * pageSize;
}
