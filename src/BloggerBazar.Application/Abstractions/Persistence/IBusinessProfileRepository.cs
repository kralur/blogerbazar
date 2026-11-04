using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IBusinessProfileRepository
{
    Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    // Audit 2026-10-22: the same conditional move out of "pending" as for bloggers.
    Task<bool> TryDecidePendingAsync(Guid id, BloggerStatus status, CancellationToken cancellationToken) => Task.FromResult(true);
    Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<BusinessProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
    Task<BusinessProfile?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult<BusinessProfile?>(null);
    Task<IReadOnlyList<BusinessProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BusinessProfile>>([]);
    Task<IReadOnlyList<BusinessProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BusinessProfile>>([]);
    Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken);
}
