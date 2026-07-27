using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class DealRepository(BloggerBazarDbContext dbContext) : IDealRepository
{
    public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Deals.SingleOrDefaultAsync(deal => deal.Id == id, cancellationToken);

    public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) =>
        dbContext.Deals.AnyAsync(deal => deal.CampaignApplicationId == campaignApplicationId, cancellationToken);

    public async Task<IReadOnlyList<Deal>> GetForParticipantsAsync(Guid? bloggerId, Guid? businessId, CancellationToken cancellationToken)
    {
        if (bloggerId is null && businessId is null)
        {
            return [];
        }

        var query = dbContext.Deals.AsNoTracking()
            .Include(deal => deal.CampaignApplication).ThenInclude(application => application!.Campaign)
            .Include(deal => deal.CollaborationRequest)
            .Include(deal => deal.Blogger)
            .Include(deal => deal.Business)
            .Include(deal => deal.Reviews)
            .AsQueryable();

        if (bloggerId is not null && businessId is not null)
        {
            query = query.Where(deal => deal.BloggerId == bloggerId || deal.BusinessId == businessId);
        }
        else if (bloggerId is not null)
        {
            query = query.Where(deal => deal.BloggerId == bloggerId);
        }
        else
        {
            query = query.Where(deal => deal.BusinessId == businessId);
        }

        return await query.OrderByDescending(deal => deal.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Deal deal, CancellationToken cancellationToken) => await dbContext.Deals.AddAsync(deal, cancellationToken);
}
