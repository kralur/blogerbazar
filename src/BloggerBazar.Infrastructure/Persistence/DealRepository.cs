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

    public async Task AddAsync(Deal deal, CancellationToken cancellationToken) => await dbContext.Deals.AddAsync(deal, cancellationToken);
}
