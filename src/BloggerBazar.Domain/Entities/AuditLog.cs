namespace BloggerBazar.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog() { }
    private AuditLog(long actorTelegramUserId, string action, string targetType, string targetId, string? details)
    {
        Id = Guid.NewGuid(); ActorTelegramUserId = actorTelegramUserId; Action = action; TargetType = targetType; TargetId = targetId; Details = details; CreatedAtUtc = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public long ActorTelegramUserId { get; private set; }
    public string Action { get; private set; } = null!;
    public string TargetType { get; private set; } = null!;
    public string TargetId { get; private set; } = null!;
    public string? Details { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static AuditLog Create(long actorTelegramUserId, string action, string targetType, string targetId, string? details = null) => new(actorTelegramUserId, action, targetType, targetId, details);
}
