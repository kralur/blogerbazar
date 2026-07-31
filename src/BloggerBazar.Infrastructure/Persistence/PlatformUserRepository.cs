using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class PlatformUserRepository(BloggerBazarDbContext dbContext) : IPlatformUserRepository
{
    public Task<PlatformUser?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.PlatformUsers.SingleOrDefaultAsync(user => user.TelegramUserId == telegramUserId, cancellationToken);

    public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
        dbContext.PlatformUsers.CountAsync(user => !user.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<PlatformUser>> GetActiveAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers.Where(user => !user.IsDeleted).OrderByDescending(user => user.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlatformUser>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers.OrderByDescending(user => user.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<bool> SoftDeleteIfActiveAsync(long telegramUserId, long deletedByTelegramUserId, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers
            .Where(user => user.TelegramUserId == telegramUserId && !user.IsDeleted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.IsDeleted, true)
                .SetProperty(user => user.DeletedAtUtc, DateTime.UtcNow)
                .SetProperty(user => user.DeletedByTelegramUserId, deletedByTelegramUserId)
                .SetProperty(user => user.UpdatedAtUtc, DateTime.UtcNow), cancellationToken) == 1;

    public async Task AddAsync(PlatformUser user, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers.AddAsync(user, cancellationToken);
}
