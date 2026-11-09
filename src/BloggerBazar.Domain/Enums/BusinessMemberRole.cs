namespace BloggerBazar.Domain.Enums;

// D64: who a person is in a company's team. Stored as a number; the API speaks camelCase strings (D26).
// Exactly one active Owner per company; the order is the order of rights (Owner has all of them).
public enum BusinessMemberRole
{
    Owner = 0,
    Admin = 1,
    Manager = 2,
    Viewer = 3
}
