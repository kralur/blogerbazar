namespace BloggerBazar.Domain.Enums;

// D64 stage C: an invitation link to a company's team. Open until somebody asks for access with it (one link, one
// person); then the owner or an admin accepts or declines. Revoked: closed by the company before anybody used it.
public enum BusinessInvitationStatus
{
    Open = 0,
    Requested = 1,
    Accepted = 2,
    Declined = 3,
    Revoked = 4
}
