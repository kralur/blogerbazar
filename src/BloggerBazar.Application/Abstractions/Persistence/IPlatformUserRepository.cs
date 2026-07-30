using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IPlatformUserRepository
{
    Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken);
    Task<int> CountActiveAsync(CancellationToken cancellationToken);
    Task AddAsync(PlatformUser user, CancellationToken cancellationToken);
}
