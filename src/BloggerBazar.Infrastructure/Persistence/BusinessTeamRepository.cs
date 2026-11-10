using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BusinessTeamRepository(BloggerBazarDbContext dbContext) : IBusinessTeamRepository
{
    public async Task<IReadOnlyList<TeamMemberRow>> GetMembersAsync(Guid businessId, CancellationToken cancellationToken) =>
        await (from member in dbContext.BusinessMembers.AsNoTracking()
               where member.BusinessId == businessId && member.RemovedAtUtc == null
               join user in dbContext.PlatformUsers.AsNoTracking() on member.TelegramUserId equals user.TelegramUserId into users
               from user in users.DefaultIfEmpty()
               orderby member.Role, member.JoinedAtUtc
               select new TeamMemberRow(member.Id, member.TelegramUserId, user == null ? "" : user.FirstName, user == null ? null : user.Username, member.Role, member.JoinedAtUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<TeamInvitationRow>> GetPendingInvitationsAsync(Guid businessId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await (from invitation in dbContext.BusinessInvitations.AsNoTracking()
               where invitation.BusinessId == businessId
                     && (invitation.Status == BusinessInvitationStatus.Requested
                         || (invitation.Status == BusinessInvitationStatus.Open && invitation.ExpiresAtUtc > nowUtc))
               join user in dbContext.PlatformUsers.AsNoTracking() on invitation.RequestedByTelegramUserId equals (long?)user.TelegramUserId into users
               from user in users.DefaultIfEmpty()
               orderby invitation.Status descending, invitation.CreatedAtUtc descending
               select new TeamInvitationRow(
                   invitation.Id, invitation.Role, invitation.Status, invitation.CreatedAtUtc, invitation.ExpiresAtUtc,
                   invitation.RequestedByTelegramUserId,
                   user == null ? null : user.FirstName,
                   user == null ? null : user.Username,
                   user == null ? null : user.VerifiedPhone,
                   invitation.RequestedAtUtc))
            .ToArrayAsync(cancellationToken);

    public Task<int> CountActiveMembersAsync(Guid businessId, CancellationToken cancellationToken) =>
        dbContext.BusinessMembers.CountAsync(member => member.BusinessId == businessId && member.RemovedAtUtc == null, cancellationToken);

    public Task<BusinessMember?> GetActiveMemberAsync(Guid businessId, Guid memberId, CancellationToken cancellationToken) =>
        dbContext.BusinessMembers.SingleOrDefaultAsync(member => member.Id == memberId && member.BusinessId == businessId && member.RemovedAtUtc == null, cancellationToken);

    public Task<BusinessMember?> GetActiveMembershipAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BusinessMembers.SingleOrDefaultAsync(member => member.TelegramUserId == telegramUserId && member.RemovedAtUtc == null, cancellationToken);

    public async Task<IReadOnlyList<long>> GetDecidersAsync(Guid businessId, CancellationToken cancellationToken) =>
        await dbContext.BusinessMembers.AsNoTracking()
            .Where(member => member.BusinessId == businessId && member.RemovedAtUtc == null
                && (member.Role == BusinessMemberRole.Owner || member.Role == BusinessMemberRole.Admin))
            .Select(member => member.TelegramUserId)
            .ToArrayAsync(cancellationToken);

    public async Task AddMemberAsync(BusinessMember member, CancellationToken cancellationToken) =>
        await dbContext.BusinessMembers.AddAsync(member, cancellationToken);

    public async Task AddInvitationAsync(BusinessInvitation invitation, CancellationToken cancellationToken) =>
        await dbContext.BusinessInvitations.AddAsync(invitation, cancellationToken);

    public async Task LockCompanyAsync(Guid businessId, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM business_profiles WHERE \"Id\" = {businessId} FOR UPDATE", cancellationToken);

    public async Task<bool> TryRequestAsync(Guid invitationId, long telegramUserId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await dbContext.BusinessInvitations
            .Where(invitation => invitation.Id == invitationId && invitation.Status == BusinessInvitationStatus.Open && invitation.ExpiresAtUtc > nowUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(invitation => invitation.Status, BusinessInvitationStatus.Requested)
                .SetProperty(invitation => invitation.RequestedByTelegramUserId, telegramUserId)
                .SetProperty(invitation => invitation.RequestedAtUtc, nowUtc), cancellationToken) == 1;

    public Task<BusinessInvitation?> GetInvitationByCodeHashAsync(string codeHash, CancellationToken cancellationToken) =>
        dbContext.BusinessInvitations.Include(invitation => invitation.Business)
            .SingleOrDefaultAsync(invitation => invitation.CodeHash == codeHash && !invitation.Business.IsDeleted, cancellationToken);

    public Task<BusinessInvitation?> GetInvitationAsync(Guid businessId, Guid invitationId, CancellationToken cancellationToken) =>
        dbContext.BusinessInvitations.SingleOrDefaultAsync(invitation => invitation.Id == invitationId && invitation.BusinessId == businessId, cancellationToken);

    public async Task CloseInvitationsAsync(Guid businessId, long closedByTelegramUserId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var pending = await dbContext.BusinessInvitations
            .Where(invitation => invitation.BusinessId == businessId
                && (invitation.Status == BusinessInvitationStatus.Open || invitation.Status == BusinessInvitationStatus.Requested))
            .ToListAsync(cancellationToken);
        foreach (var invitation in pending)
        {
            if (!invitation.Revoke(closedByTelegramUserId, nowUtc)) invitation.Decline(closedByTelegramUserId, nowUtc);
        }
    }
}
