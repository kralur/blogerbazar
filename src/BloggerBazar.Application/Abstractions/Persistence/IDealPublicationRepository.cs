using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IDealPublicationRepository
{
    Task<IReadOnlyList<DealPublication>> ListForDealAsync(Guid dealId, CancellationToken cancellationToken);
    Task<DealPublication?> GetForDealAsync(Guid dealId, Guid publicationId, CancellationToken cancellationToken);
    Task AddAsync(DealPublication publication, CancellationToken cancellationToken);
    void Remove(DealPublication publication);
}
