using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

// D64: a person in a company's team. The company's owner is a member too (role Owner); everybody else joins by an
// invitation the owner approves (stage C). One person works for at most one company (an active membership is unique
// per Telegram user), so "my company" stays unambiguous in the Mini App and the browser.
public sealed class BusinessMember
{
    private BusinessMember() { }

    private BusinessMember(Guid businessId, long telegramUserId, BusinessMemberRole role, long? invitedByTelegramUserId, DateTime joinedAtUtc)
    {
        Id = Guid.NewGuid();
        BusinessId = businessId;
        TelegramUserId = telegramUserId;
        Role = role;
        InvitedByTelegramUserId = invitedByTelegramUserId;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public long TelegramUserId { get; private set; }
    public BusinessMemberRole Role { get; private set; }
    public long? InvitedByTelegramUserId { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    // Removed members keep their row for the journal; only active rows give access.
    public DateTime? RemovedAtUtc { get; private set; }
    public bool IsActive => RemovedAtUtc is null;

    public static BusinessMember Owner(Guid businessId, long telegramUserId, DateTime joinedAtUtc) =>
        new(businessId, telegramUserId, BusinessMemberRole.Owner, null, joinedAtUtc);

    public static BusinessMember Join(Guid businessId, long telegramUserId, BusinessMemberRole role, long invitedByTelegramUserId, DateTime joinedAtUtc) =>
        role == BusinessMemberRole.Owner
            ? throw new InvalidOperationException("A company gets its owner only from its creation or an ownership transfer.")
            : new(businessId, telegramUserId, role, invitedByTelegramUserId, joinedAtUtc);

    public void Remove(DateTime removedAtUtc) => RemovedAtUtc ??= removedAtUtc;

    // The table of rights in docs/business-teams.md: Viewer sees, Manager works, Admin also runs the profile and the
    // team, the Owner also owns (plan, transfer, deletion).
    public static bool Allows(BusinessMemberRole role, BusinessPermission permission) => permission switch
    {
        BusinessPermission.View => true,
        BusinessPermission.Operate => role is BusinessMemberRole.Owner or BusinessMemberRole.Admin or BusinessMemberRole.Manager,
        BusinessPermission.ManageProfile or BusinessPermission.ManageTeam => role is BusinessMemberRole.Owner or BusinessMemberRole.Admin,
        BusinessPermission.Own => role is BusinessMemberRole.Owner,
        _ => false
    };

    // A returning owner (company revived after the account came back) is active again.
    public void Reactivate() => RemovedAtUtc = null;
}
