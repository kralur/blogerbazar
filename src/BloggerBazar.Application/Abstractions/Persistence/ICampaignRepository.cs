using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignRepository
{
    Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<Campaign>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Campaign>>([]);
    Task AddAsync(Campaign campaign, CancellationToken cancellationToken);
    // Deletes the owner's campaign only while it has no applications; false when nothing was deleted.
    Task<bool> DeleteWithoutApplicationsAsync(Guid id, Guid businessId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
