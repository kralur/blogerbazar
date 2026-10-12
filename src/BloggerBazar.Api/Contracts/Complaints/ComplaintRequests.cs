namespace BloggerBazar.Api.Contracts.Complaints;

public sealed record CreateComplaintRequest(string TargetType, Guid TargetId, string Reason, string? Comment);

public sealed record CloseComplaintRequest(string Status, string? Note);
