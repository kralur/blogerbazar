using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CollaborationRequestRepository(BloggerBazarDbContext dbContext) : ICollaborationRequestRepository
{
    public Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CollaborationRequests.Include(request => request.Blogger).Include(request => request.Business)
            .SingleOrDefaultAsync(request => request.Id == id, cancellationToken);

    public Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken) =>
        dbContext.Deals.AnyAsync(deal => deal.CollaborationRequestId == requestId, cancellationToken);

    public async Task<IReadOnlyList<CollaborationRequest>> GetForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken) =>
        await dbContext.CollaborationRequests.AsNoTracking().Include(request => request.Business).Include(request => request.Deal)
            .Where(request => request.BloggerId == bloggerId).OrderByDescending(request => request.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CollaborationRequest>> GetForBusinessAsync(Guid businessId, CancellationToken cancellationToken) =>
        await dbContext.CollaborationRequests.AsNoTracking().Include(request => request.Blogger).Include(request => request.Deal)
            .Where(request => request.BusinessId == businessId).OrderByDescending(request => request.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CollaborationRequest>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.CollaborationRequests.AsNoTracking().Include(request => request.Blogger).Include(request => request.Business)
            .OrderByDescending(request => request.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task AddAsync(CollaborationRequest request, CancellationToken cancellationToken) =>
        await dbContext.CollaborationRequests.AddAsync(request, cancellationToken);
}
