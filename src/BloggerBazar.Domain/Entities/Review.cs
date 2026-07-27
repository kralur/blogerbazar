using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class Review
{
    private Review() { }

    private Review(Guid dealId, long reviewerTelegramUserId, ReviewTargetType targetType, Guid? bloggerId, Guid? businessId, int rating, string? comment)
    {
        Id = Guid.NewGuid();
        DealId = dealId;
        ReviewerTelegramUserId = reviewerTelegramUserId;
        TargetType = targetType;
        BloggerId = bloggerId;
        BusinessId = businessId;
        Rating = rating;
        Comment = comment;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid DealId { get; private set; }
    public Deal Deal { get; private set; } = null!;
    public long ReviewerTelegramUserId { get; private set; }
    public ReviewTargetType TargetType { get; private set; }
    public Guid? BloggerId { get; private set; }
    public BloggerProfile? Blogger { get; private set; }
    public Guid? BusinessId { get; private set; }
    public BusinessProfile? Business { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Review ForBlogger(Guid dealId, long reviewerTelegramUserId, Guid bloggerId, int rating, string? comment) =>
        new(dealId, reviewerTelegramUserId, ReviewTargetType.Blogger, bloggerId, null, rating, comment);

    public static Review ForBusiness(Guid dealId, long reviewerTelegramUserId, Guid businessId, int rating, string? comment) =>
        new(dealId, reviewerTelegramUserId, ReviewTargetType.Business, null, businessId, rating, comment);
}
