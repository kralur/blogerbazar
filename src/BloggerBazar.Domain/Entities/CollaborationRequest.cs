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
    public CollaborationFormat? Format { get; private set; }
    public int? OfferedBudget { get; private set; }
    public DateTime? Deadline { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public Deal? Deal { get; private set; }

    public static readonly TimeSpan OfferLifetime = TimeSpan.FromHours(48);

    public bool IsOffer => ExpiresAtUtc.HasValue;
    public bool IsPending => Status is CollaborationRequestStatus.Sent or CollaborationRequestStatus.Viewed;

    public static CollaborationRequest Create(Guid bloggerId, Guid businessId, string message) => new(bloggerId, businessId, message);

    public static CollaborationRequest CreateOffer(Guid bloggerId, Guid businessId, string message, CollaborationFormat format, int? offeredBudget, DateTime? deadline)
    {
        if (offeredBudget < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offeredBudget), "Offered budget cannot be negative.");
        }

        var offer = new CollaborationRequest(bloggerId, businessId, message)
        {
            Format = format,
            OfferedBudget = offeredBudget,
            Deadline = deadline
        };
        offer.ExpiresAtUtc = offer.CreatedAtUtc.Add(OfferLifetime);
        return offer;
    }

    public bool IsExpiredAt(DateTime utcNow) => IsPending && ExpiresAtUtc <= utcNow;

    public void Expire() => TransitionTo(CollaborationRequestStatus.Expired);

    public void MarkViewed() => TransitionTo(CollaborationRequestStatus.Viewed);

    public void Accept() => TransitionTo(CollaborationRequestStatus.Accepted);

    public void Decline() => TransitionTo(CollaborationRequestStatus.Declined);

    private void TransitionTo(CollaborationRequestStatus status)
    {
        if (!IsPending)
        {
            throw new InvalidOperationException("A final collaboration request cannot be changed.");
        }

        Status = status;
    }
}
