using System.Linq.Expressions;
using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CollaborationRequestRepository(BloggerBazarDbContext dbContext) : ICollaborationRequestRepository
{
    public Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CollaborationRequests.Include(request => request.Blogger).Include(request => request.Business)
            .SingleOrDefaultAsync(request => request.Id == id, cancellationToken);

    public Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken) =>
        dbContext.Deals.AnyAsync(deal => deal.CollaborationRequestId == requestId, cancellationToken);

    public async Task AddAsync(CollaborationRequest request, CancellationToken cancellationToken) =>
        await dbContext.CollaborationRequests.AddAsync(request, cancellationToken);

    public Task<CollaborationRequest?> GetOfferForParticipantAsync(Guid offerId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken) =>
        Offers()
            .Where(request => request.Id == offerId)
            .Where(ParticipantFilter(role, profileId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CollaborationRequest>> ListOffersForParticipantAsync(MarketplaceRole role, Guid profileId, int take, CancellationToken cancellationToken) =>
        await Offers().AsNoTracking()
            .Where(ParticipantFilter(role, profileId))
            .OrderByDescending(request => request.CreatedAtUtc)
            .ThenByDescending(request => request.Id)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public Task<CollaborationRequest?> GetPendingOfferAsync(Guid businessId, Guid bloggerId, CancellationToken cancellationToken) =>
        dbContext.CollaborationRequests.SingleOrDefaultAsync(request => request.BusinessId == businessId
            && request.BloggerId == bloggerId
            && request.ExpiresAtUtc != null
            && (request.Status == CollaborationRequestStatus.Sent || request.Status == CollaborationRequestStatus.Viewed), cancellationToken);

    public Task<int> CountOffersSinceAsync(Guid businessId, DateTime sinceUtc, CancellationToken cancellationToken) =>
        dbContext.CollaborationRequests.CountAsync(request => request.BusinessId == businessId
            && request.ExpiresAtUtc != null
            && request.CreatedAtUtc >= sinceUtc, cancellationToken);

    private IQueryable<CollaborationRequest> Offers() =>
        dbContext.CollaborationRequests
            .Include(request => request.Blogger)
            .Include(request => request.Business)
            .Include(request => request.Deal)
            .Where(request => request.ExpiresAtUtc != null && !request.Blogger.IsDeleted && !request.Business.IsDeleted);

    private static Expression<Func<CollaborationRequest, bool>> ParticipantFilter(MarketplaceRole role, Guid profileId) => role switch
    {
        MarketplaceRole.Blogger => request => request.BloggerId == profileId,
        MarketplaceRole.Business => request => request.BusinessId == profileId,
        _ => request => false
    };
}
