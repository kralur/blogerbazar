using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class DealRepository(BloggerBazarDbContext dbContext) : IDealRepository
{
    public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Deals.SingleOrDefaultAsync(deal => deal.Id == id, cancellationToken);

    public Task<Deal?> GetByCampaignApplicationIdAsync(Guid campaignApplicationId, CancellationToken cancellationToken) =>
        dbContext.Deals.SingleOrDefaultAsync(deal => deal.CampaignApplicationId == campaignApplicationId, cancellationToken);

    public Task<Deal?> GetByCollaborationRequestIdAsync(Guid collaborationRequestId, CancellationToken cancellationToken) =>
        dbContext.Deals.SingleOrDefaultAsync(deal => deal.CollaborationRequestId == collaborationRequestId, cancellationToken);

    public Task<Deal?> GetForParticipantAsync(Guid dealId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken) =>
        dbContext.Deals.AsNoTracking()
            .Include(deal => deal.Blogger)
            .Include(deal => deal.Business)
            .Where(deal => deal.Id == dealId)
            .Where(DealParticipantFilter.For(role, profileId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsBetweenAsync(Guid bloggerId, Guid businessId, CancellationToken cancellationToken) =>
        dbContext.Deals.AnyAsync(deal => deal.BloggerId == bloggerId && deal.BusinessId == businessId
            && !deal.Blogger.IsDeleted && !deal.Business.IsDeleted, cancellationToken);

    public async Task<bool> TryCompleteAsync(Guid dealId, DateTime completedAtUtc, CancellationToken cancellationToken) =>
        await dbContext.Deals
            .Where(deal => deal.Id == dealId && deal.Status == DealStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(deal => deal.Status, DealStatus.Completed)
                .SetProperty(deal => deal.CompletedAtUtc, (DateTime?)completedAtUtc), cancellationToken) == 1;

    public Task<bool> ExistsForApplicationAsync(Guid campaignApplicationId, CancellationToken cancellationToken) =>
        dbContext.Deals.AnyAsync(deal => deal.CampaignApplicationId == campaignApplicationId, cancellationToken);

    public async Task AddAsync(Deal deal, CancellationToken cancellationToken) => await dbContext.Deals.AddAsync(deal, cancellationToken);
}
