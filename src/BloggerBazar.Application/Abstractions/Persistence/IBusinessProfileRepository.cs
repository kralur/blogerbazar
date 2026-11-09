using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

// D64: the company a person works for and their role in its team.
public sealed record BusinessMembership(BusinessProfile Business, BusinessMemberRole Role)
{
    public bool Allows(BusinessPermission permission) => BusinessMember.Allows(Role, permission);
}

public interface IBusinessProfileRepository
{
    Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    // Audit 2026-10-22: the same conditional move out of "pending" as for bloggers.
    Task<bool> TryDecidePendingAsync(Guid id, BloggerStatus status, CancellationToken cancellationToken) => Task.FromResult(true);
    Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<BusinessProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
    Task<BusinessProfile?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult<BusinessProfile?>(null);
    Task<IReadOnlyList<BusinessProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BusinessProfile>>([]);
    Task<IReadOnlyList<BusinessProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BusinessProfile>>([]);
    Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken);

    // D64: "my company" is the company of the caller's active membership, not the profile keyed by their Telegram id.
    // The defaults describe a company without a team (its owner is its only member); the real repository reads
    // business_members.
    async Task<BusinessMembership?> GetMembershipAsync(long telegramUserId, CancellationToken cancellationToken) =>
        await GetByTelegramUserIdAsync(telegramUserId, cancellationToken) is { } owned ? new BusinessMembership(owned, BusinessMemberRole.Owner) : null;

    // Whether the person is in this company's team (self-dealing checks: no offer to, application to or complaint
    // about your own company).
    async Task<bool> IsMemberAsync(Guid businessId, long telegramUserId, CancellationToken cancellationToken) =>
        (await GetByIdAsync(businessId, cancellationToken))?.TelegramUserId == telegramUserId;

    // A created or revived company has its owner as an active member (saved with the same unit of work).
    Task EnsureOwnerMemberAsync(BusinessProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;

    // Deleting a company or an account ends the other members' access.
    Task RemoveOtherMembersAsync(Guid businessId, long ownerTelegramUserId, DateTime removedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    Task RemoveMembershipAsync(long telegramUserId, DateTime removedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
}
