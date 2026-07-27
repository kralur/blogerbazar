using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class CollaborationRequest
{
    private CollaborationRequest() { }

    private CollaborationRequest(Guid bloggerId, Guid businessId, string message)
    {
        Id = Guid.NewGuid();
        BloggerId = bloggerId;
        BusinessId = businessId;
        Message = message;
        Status = CollaborationRequestStatus.Sent;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid BloggerId { get; private set; }
    public BloggerProfile Blogger { get; private set; } = null!;
    public Guid BusinessId { get; private set; }
    public BusinessProfile Business { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public CollaborationRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Deal? Deal { get; private set; }

    public static CollaborationRequest Create(Guid bloggerId, Guid businessId, string message) => new(bloggerId, businessId, message);

    public void MarkViewed() => TransitionTo(CollaborationRequestStatus.Viewed);

    public void Accept() => TransitionTo(CollaborationRequestStatus.Accepted);

    public void Decline() => TransitionTo(CollaborationRequestStatus.Declined);

    private void TransitionTo(CollaborationRequestStatus status)
    {
        if (Status is CollaborationRequestStatus.Accepted or CollaborationRequestStatus.Declined)
        {
            throw new InvalidOperationException("A final collaboration request cannot be changed.");
        }

        Status = status;
    }
}
