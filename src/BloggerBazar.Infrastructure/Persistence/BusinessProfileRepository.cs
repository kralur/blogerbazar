using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BusinessProfileRepository(BloggerBazarDbContext dbContext) : IBusinessProfileRepository
{
    public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.TelegramUserId == telegramUserId, cancellationToken);

    public Task<BusinessProfile?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.Username == username, cancellationToken);

    public async Task<IReadOnlyList<BusinessProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BusinessProfiles.AsNoTracking()
            .OrderByDescending(profile => profile.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BusinessProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BusinessProfiles.Where(profile => profile.ModerationStatus == BloggerStatus.Pending)
            .OrderBy(profile => profile.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => await dbContext.BusinessProfiles.AddAsync(profile, cancellationToken);
}
