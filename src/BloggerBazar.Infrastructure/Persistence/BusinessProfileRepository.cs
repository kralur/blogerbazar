using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BusinessProfileRepository(BloggerBazarDbContext dbContext) : IBusinessProfileRepository
{
    public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.TelegramUserId == telegramUserId, cancellationToken);

    public async Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken) => await dbContext.BusinessProfiles.AddAsync(profile, cancellationToken);
}
