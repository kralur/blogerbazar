using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

// D64 stage C: a one-time link into a company's team, valid for 48 hours. Only a hash of its code is stored. The first
// person to ask for access with it takes it; the owner or an admin then sees their name and number and decides.
public sealed class BusinessInvitation
{
    private BusinessInvitation() { }

    private BusinessInvitation(Guid businessId, string codeHash, BusinessMemberRole role, long createdByTelegramUserId, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        Id = Guid.NewGuid();
        BusinessId = businessId;
        CodeHash = codeHash;
        Role = role;
        CreatedByTelegramUserId = createdByTelegramUserId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = BusinessInvitationStatus.Open;
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public BusinessProfile Business { get; private set; } = null!;
    public string CodeHash { get; private set; } = null!;
    public BusinessMemberRole Role { get; private set; }
    public BusinessInvitationStatus Status { get; private set; }
    public long CreatedByTelegramUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    // Asking for access is possible until then; a request already made waits for the decision.
    public DateTime ExpiresAtUtc { get; private set; }
    public long? RequestedByTelegramUserId { get; private set; }
    public DateTime? RequestedAtUtc { get; private set; }
    public long? DecidedByTelegramUserId { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }

    public static BusinessInvitation Create(Guid businessId, string codeHash, BusinessMemberRole role, long createdByTelegramUserId, DateTime nowUtc, TimeSpan lifetime) =>
        role == BusinessMemberRole.Owner
            ? throw new InvalidOperationException("Nobody is invited as the owner; ownership is transferred.")
            : new(businessId, codeHash, role, createdByTelegramUserId, nowUtc, nowUtc.Add(lifetime));

    public bool IsOpenAt(DateTime nowUtc) => Status == BusinessInvitationStatus.Open && ExpiresAtUtc > nowUtc;

    public bool Request(long telegramUserId, DateTime nowUtc)
    {
        if (!IsOpenAt(nowUtc)) return false;
        Status = BusinessInvitationStatus.Requested;
        RequestedByTelegramUserId = telegramUserId;
        RequestedAtUtc = nowUtc;
        return true;
    }

    public bool Accept(long decidedByTelegramUserId, DateTime nowUtc) => Decide(BusinessInvitationStatus.Accepted, decidedByTelegramUserId, nowUtc, BusinessInvitationStatus.Requested);

    public bool Decline(long decidedByTelegramUserId, DateTime nowUtc) => Decide(BusinessInvitationStatus.Declined, decidedByTelegramUserId, nowUtc, BusinessInvitationStatus.Requested);

    public bool Revoke(long decidedByTelegramUserId, DateTime nowUtc) => Decide(BusinessInvitationStatus.Revoked, decidedByTelegramUserId, nowUtc, BusinessInvitationStatus.Open);

    private bool Decide(BusinessInvitationStatus status, long decidedByTelegramUserId, DateTime nowUtc, BusinessInvitationStatus from)
    {
        if (Status != from) return false;
        Status = status;
        DecidedByTelegramUserId = decidedByTelegramUserId;
        DecidedAtUtc = nowUtc;
        return true;
    }
}
