using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class DealPublicationRepository(BloggerBazarDbContext dbContext) : IDealPublicationRepository
{
    public async Task<IReadOnlyList<DealPublication>> ListForDealAsync(Guid dealId, CancellationToken cancellationToken) =>
        await dbContext.DealPublications.AsNoTracking()
            .Where(publication => publication.DealId == dealId)
            .OrderBy(publication => publication.CreatedAtUtc)
            .ThenBy(publication => publication.Id)
            .ToArrayAsync(cancellationToken);

    public Task<DealPublication?> GetForDealAsync(Guid dealId, Guid publicationId, CancellationToken cancellationToken) =>
        dbContext.DealPublications.SingleOrDefaultAsync(publication => publication.Id == publicationId && publication.DealId == dealId, cancellationToken);

    public async Task AddAsync(DealPublication publication, CancellationToken cancellationToken) =>
        await dbContext.DealPublications.AddAsync(publication, cancellationToken);

    public void Remove(DealPublication publication) => dbContext.DealPublications.Remove(publication);
}
