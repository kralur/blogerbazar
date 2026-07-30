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

    public async Task AddAsync(PlatformUser user, CancellationToken cancellationToken) =>
        await dbContext.PlatformUsers.AddAsync(user, cancellationToken);
}
