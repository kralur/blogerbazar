using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

// A user's report about a profile, campaign, offer or deal (D52). It only queues the case for an admin;
// nothing is blocked automatically. Target type, reason and status are fixed camelCase strings (D26).
public sealed class Complaint
{
    public const string OpenStatus = "open";

    private Complaint() { }

    private Complaint(long reporterTelegramUserId, MarketplaceRole reporterRole, string targetType, Guid targetId, string reason, string? comment, DateTime utcNow)
    {
        Id = Guid.NewGuid();
        ReporterTelegramUserId = reporterTelegramUserId;
        ReporterRole = reporterRole;
        TargetType = targetType;
        TargetId = targetId;
        Reason = reason;
        Comment = comment;
        Status = OpenStatus;
        CreatedAtUtc = utcNow;
    }

    public Guid Id { get; private set; }
    public long ReporterTelegramUserId { get; private set; }
    public MarketplaceRole ReporterRole { get; private set; }
    public string TargetType { get; private set; } = null!;
    public Guid TargetId { get; private set; }
    public string Reason { get; private set; } = null!;
    public string? Comment { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public long? ResolvedByTelegramUserId { get; private set; }
    public string? ResolutionNote { get; private set; }

    public static Complaint Create(long reporterTelegramUserId, MarketplaceRole reporterRole, string targetType, Guid targetId, string reason, string? comment, DateTime utcNow) =>
        new(reporterTelegramUserId, reporterRole, targetType, targetId, reason, comment, utcNow);

    public void Close(string status, long adminTelegramUserId, string? note, DateTime utcNow)
    {
        if (Status != OpenStatus) throw new InvalidOperationException("The complaint is already closed.");
        if (status == OpenStatus) throw new ArgumentException("A complaint closes as resolved or dismissed.", nameof(status));
        Status = status;
        ResolvedByTelegramUserId = adminTelegramUserId;
        ResolutionNote = note;
        ResolvedAtUtc = utcNow;
    }
}
