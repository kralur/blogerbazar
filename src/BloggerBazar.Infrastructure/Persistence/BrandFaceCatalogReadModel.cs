using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.BrandFaces;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal static class BrandFaceCatalogVisibility
{
    internal static IQueryable<BrandFaceProfile> PublicBrandFaces(
        IQueryable<BrandFaceProfile> profiles,
        IQueryable<PlatformUser> platformUsers) =>
        profiles.Where(profile => !profile.IsDeleted
            && !platformUsers.Any(user => user.TelegramUserId == profile.TelegramUserId && (user.IsBlocked || user.IsDeleted)));
}

internal sealed class BrandFaceCatalogReadModel(BloggerBazarDbContext dbContext) : IBrandFaceCatalogReadModel
{
    public async Task<BrandFaceCatalogResult> SearchAsync(BrandFaceCatalogSearch search, CancellationToken cancellationToken)
    {
        var query = BrandFaceCatalogVisibility.PublicBrandFaces(
            dbContext.BrandFaceProfiles.AsNoTracking(),
            dbContext.PlatformUsers.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var pattern = PostgresSearchPattern.Contains(search.Query);
            query = query.Where(profile => EF.Functions.ILike(profile.Name, pattern)
                || EF.Functions.ILike(profile.City, pattern)
                || profile.Categories.Any(category => EF.Functions.ILike(category, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(search.City))
        {
            query = query.Where(profile => profile.City == search.City);
        }

        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            query = query.Where(profile => profile.Categories.Contains(search.Category));
        }

        if (!string.IsNullOrWhiteSpace(search.Language))
        {
            query = query.Where(profile => profile.Languages.Contains(search.Language));
        }

        if (search.MinPrice.HasValue)
        {
            query = query.Where(profile => profile.CollaborationPrice != null && profile.CollaborationPrice >= search.MinPrice.Value);
        }

        if (search.MaxPrice.HasValue)
        {
            query = query.Where(profile => profile.CollaborationPrice != null && profile.CollaborationPrice <= search.MaxPrice.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = BrandFaceCatalogSorting.Apply(query, search.Sort);

        var items = await ordered
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .Select(profile => new BrandFaceCatalogItemDto(
                profile.Id,
                profile.Name,
                profile.City,
                profile.Languages,
                profile.Categories,
                profile.CollaborationPrice,
                profile.AvatarUrl,
                profile.IsPromoted,
                profile.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new BrandFaceCatalogResult(items, total, search.Page, search.PageSize, BrandFaceCatalogPagination.HasMore(total, search.Page, search.PageSize));
    }
}

internal static class BrandFaceCatalogSorting
{
    internal static IOrderedQueryable<BrandFaceProfile> Apply(IQueryable<BrandFaceProfile> query, string sort) =>
        sort switch
        {
            "newest" => query.OrderByDescending(profile => profile.CreatedAtUtc).ThenBy(profile => profile.Id),
            "price_asc" => query.OrderBy(profile => profile.CollaborationPrice == null).ThenBy(profile => profile.CollaborationPrice).ThenBy(profile => profile.Id),
            "price_desc" => query.OrderBy(profile => profile.CollaborationPrice == null).ThenByDescending(profile => profile.CollaborationPrice).ThenBy(profile => profile.Id),
            _ => query.OrderByDescending(profile => profile.IsPromoted).ThenByDescending(profile => profile.UpdatedAtUtc).ThenBy(profile => profile.Id)
        };
}

internal static class BrandFaceCatalogPagination
{
    internal static bool HasMore(int total, int page, int pageSize) => total > (long)page * pageSize;
}
