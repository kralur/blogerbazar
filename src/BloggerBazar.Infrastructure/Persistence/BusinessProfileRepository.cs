using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BusinessProfileRepository(BloggerBazarDbContext dbContext) : IBusinessProfileRepository
{
    public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.Id == id && !profile.IsDeleted, cancellationToken);

    public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.TelegramUserId == telegramUserId && !profile.IsDeleted, cancellationToken);

    public Task<BusinessProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.TelegramUserId == telegramUserId, cancellationToken);

    public Task<BusinessProfile?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.Username == username && !profile.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<BusinessProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BusinessProfiles.AsNoTracking().Where(profile => !profile.IsDeleted)
            .OrderByDescending(profile => profile.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BusinessProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BusinessProfiles.Where(profile => !profile.IsDeleted && profile.ModerationStatus == BloggerStatus.Pending)
            .OrderBy(profile => profile.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => await dbContext.BusinessProfiles.AddAsync(profile, cancellationToken);
}
