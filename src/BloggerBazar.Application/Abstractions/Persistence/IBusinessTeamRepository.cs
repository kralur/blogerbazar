using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

// D64 stage C: a company's team and its invitation links.
public sealed record TeamMemberRow(Guid Id, long TelegramUserId, string FirstName, string? Username, BusinessMemberRole Role, DateTime JoinedAtUtc);

// Open links and requests waiting for a decision. The requester's number is in the row; the handler shows it only to
// those who decide (owner and admins, D64 answer 5).
public sealed record TeamInvitationRow(
    Guid Id,
    BusinessMemberRole Role,
    BusinessInvitationStatus Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    long? RequestedByTelegramUserId,
    string? RequesterFirstName,
    string? RequesterUsername,
    string? RequesterPhone,
    DateTime? RequestedAtUtc);

public interface IBusinessTeamRepository
{
    Task<IReadOnlyList<TeamMemberRow>> GetMembersAsync(Guid businessId, CancellationToken cancellationToken);
    // Requests waiting for a decision, and open links that have not expired.
    Task<IReadOnlyList<TeamInvitationRow>> GetPendingInvitationsAsync(Guid businessId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<int> CountActiveMembersAsync(Guid businessId, CancellationToken cancellationToken);
    Task<BusinessMember?> GetActiveMemberAsync(Guid businessId, Guid memberId, CancellationToken cancellationToken);
    Task<BusinessMember?> GetActiveMembershipAsync(long telegramUserId, CancellationToken cancellationToken);
    // Owner and admins: who hears about a new request.
    Task<IReadOnlyList<long>> GetDecidersAsync(Guid businessId, CancellationToken cancellationToken);
    Task AddMemberAsync(BusinessMember member, CancellationToken cancellationToken);
    Task AddInvitationAsync(BusinessInvitation invitation, CancellationToken cancellationToken);
    // With the company, for the person who opened the link.
    // Inside a transaction: holds the company row until it ends, so two people deciding at once count seats one after
    // the other (the free plan's limit).
    Task LockCompanyAsync(Guid businessId, CancellationToken cancellationToken) => Task.CompletedTask;
    // Takes an open link for this person in one conditional update: of two people asking at once, one gets it.
    Task<bool> TryRequestAsync(Guid invitationId, long telegramUserId, DateTime nowUtc, CancellationToken cancellationToken);
    Task<BusinessInvitation?> GetInvitationByCodeHashAsync(string codeHash, CancellationToken cancellationToken);
    Task<BusinessInvitation?> GetInvitationAsync(Guid businessId, Guid invitationId, CancellationToken cancellationToken);
    // Closes the company's open links and pending requests (switching to "alone").
    Task CloseInvitationsAsync(Guid businessId, long closedByTelegramUserId, DateTime nowUtc, CancellationToken cancellationToken);
}
