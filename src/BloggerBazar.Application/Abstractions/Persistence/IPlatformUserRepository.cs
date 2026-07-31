using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IPlatformUserRepository
{
    Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken);
    Task<int> CountActiveAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformUser>> GetAllAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PlatformUser>>([]);
    Task<bool> SoftDeleteIfActiveAsync(long telegramUserId, long deletedByTelegramUserId, CancellationToken cancellationToken) => Task.FromResult(false);
    Task AddAsync(PlatformUser user, CancellationToken cancellationToken);
}
