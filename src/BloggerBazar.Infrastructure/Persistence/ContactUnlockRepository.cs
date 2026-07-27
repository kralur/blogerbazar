using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class ContactUnlockRepository(BloggerBazarDbContext dbContext) : IContactUnlockRepository
{
    public Task<ContactUnlock?> GetAsync(long viewerTelegramUserId, ContactTargetType targetType, Guid targetId, CancellationToken cancellationToken) =>
        dbContext.ContactUnlocks.Include(unlock => unlock.PaymentOrder).SingleOrDefaultAsync(
            unlock => unlock.ViewerTelegramUserId == viewerTelegramUserId && unlock.TargetType == targetType && unlock.TargetId == targetId,
            cancellationToken);

    public async Task AddAsync(ContactUnlock contactUnlock, CancellationToken cancellationToken) =>
        await dbContext.ContactUnlocks.AddAsync(contactUnlock, cancellationToken);
}
