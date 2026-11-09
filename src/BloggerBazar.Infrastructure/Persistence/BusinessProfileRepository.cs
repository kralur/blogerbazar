using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BusinessProfileRepository(BloggerBazarDbContext dbContext) : IBusinessProfileRepository
{
    public Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BusinessProfiles.SingleOrDefaultAsync(profile => profile.Id == id && !profile.IsDeleted, cancellationToken);

    public async Task<bool> TryDecidePendingAsync(Guid id, BloggerStatus status, CancellationToken cancellationToken) =>
        await dbContext.BusinessProfiles.Where(profile => profile.Id == id && profile.ModerationStatus == BloggerStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(profile => profile.ModerationStatus, status), cancellationToken) == 1;

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

    // D64: the company of the caller's active membership; a deleted company gives nobody access.
    public async Task<BusinessMembership?> GetMembershipAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var row = await (
            from member in dbContext.BusinessMembers
            where member.TelegramUserId == telegramUserId && member.RemovedAtUtc == null
            join business in dbContext.BusinessProfiles on member.BusinessId equals business.Id
            where !business.IsDeleted
            select new { business, member.Role }).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new BusinessMembership(row.business, row.Role);
    }

    public Task<bool> IsMemberAsync(Guid businessId, long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BusinessMembers.AnyAsync(member => member.BusinessId == businessId && member.TelegramUserId == telegramUserId && member.RemovedAtUtc == null, cancellationToken);

    public async Task EnsureOwnerMemberAsync(BusinessProfile profile, CancellationToken cancellationToken)
    {
        var owner = await dbContext.BusinessMembers.SingleOrDefaultAsync(
            member => member.BusinessId == profile.Id && member.TelegramUserId == profile.TelegramUserId && member.Role == BusinessMemberRole.Owner, cancellationToken);
        if (owner is null)
        {
            await dbContext.BusinessMembers.AddAsync(BusinessMember.Owner(profile.Id, profile.TelegramUserId, DateTime.UtcNow), cancellationToken);
        }
        else
        {
            owner.Reactivate();
        }
    }

    public Task RemoveOtherMembersAsync(Guid businessId, long ownerTelegramUserId, DateTime removedAtUtc, CancellationToken cancellationToken) =>
        dbContext.BusinessMembers
            .Where(member => member.BusinessId == businessId && member.TelegramUserId != ownerTelegramUserId && member.RemovedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(member => member.RemovedAtUtc, removedAtUtc), cancellationToken);

    public Task RemoveMembershipAsync(long telegramUserId, DateTime removedAtUtc, CancellationToken cancellationToken) =>
        dbContext.BusinessMembers
            .Where(member => member.TelegramUserId == telegramUserId && member.RemovedAtUtc == null && member.Role != BusinessMemberRole.Owner)
            .ExecuteUpdateAsync(setters => setters.SetProperty(member => member.RemovedAtUtc, removedAtUtc), cancellationToken);
}
