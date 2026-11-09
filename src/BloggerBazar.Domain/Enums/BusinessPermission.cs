namespace BloggerBazar.Domain.Enums;

// D64: what an action on a company needs. BusinessMember.Allows maps a team role to these.
public enum BusinessPermission
{
    // See the company's campaigns, applications, offers and deals.
    View,
    // Work: campaigns, decisions on applications, offers, deals (completion, price, publications, contacts, review).
    Operate,
    // The company's profile, photo and visibility.
    ManageProfile,
    // Invite and remove members (admins only by the owner).
    ManageTeam,
    // Owner only: ownership, plan, deleting the company.
    Own
}
