using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IDealReadModel
{
    Task<IReadOnlyList<DealReadRow>> ListForParticipantAsync(MarketplaceRole role, Guid profileId, CancellationToken cancellationToken);
    Task<DealReadRow?> FindForParticipantAsync(Guid dealId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken);
}
