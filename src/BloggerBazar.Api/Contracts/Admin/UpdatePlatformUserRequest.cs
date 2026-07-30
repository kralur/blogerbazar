namespace BloggerBazar.Api.Contracts.Admin;

public sealed record UpdatePlatformUserRoleRequest(int Role);
public sealed record SetPlatformUserBlockedRequest(bool IsBlocked);
