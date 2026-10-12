using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IPlatformUserRepository
{
    Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken);
    Task<int> CountActiveAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformUser>> GetAllAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([]);
    Task<bool> SoftDeleteIfActiveAsync(long telegramUserId, long deletedByTelegramUserId, CancellationToken cancellationToken) => Task.FromResult(false);
    // Active owners and admins: who hears about a new complaint (D52).
    Task<IReadOnlyList<long>> GetAdminTelegramUserIdsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<long>>([]);
    Task AddAsync(PlatformUser user, CancellationToken cancellationToken);
}
