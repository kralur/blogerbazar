using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class BusinessProfile
{
    private BusinessProfile() { }

    private BusinessProfile(long telegramUserId, string name, string? city)
    {
        Id = Guid.NewGuid();
        TelegramUserId = telegramUserId;
        Name = name;
        City = city;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public long TelegramUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Username { get; private set; }
    public string? City { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public string? Description { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsVerified { get; private set; }
    public BloggerStatus ModerationStatus { get; private set; } = BloggerStatus.Pending;
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<Campaign> Campaigns { get; private set; } = new List<Campaign>();
    public IReadOnlyCollection<Deal> Deals { get; private set; } = new List<Deal>();
    public IReadOnlyCollection<Review> Reviews { get; private set; } = new List<Review>();
    public IReadOnlyCollection<CollaborationRequest> CollaborationRequests { get; private set; } = new List<CollaborationRequest>();

    public static BusinessProfile Create(long telegramUserId, string name, string? city) => new(telegramUserId, name, city);

    public void Update(string name, string? username, string? city, string? logoUrl, string? websiteUrl, string? description, string? phone, string? email)
    {
        Name = name;
        Username = username;
        City = city;
        LogoUrl = logoUrl;
        WebsiteUrl = websiteUrl;
        Description = description;
        Phone = phone;
        Email = email;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetPrimaryImageUrl(string? logoUrl)
    {
        LogoUrl = logoUrl;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        IsVerified = false;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DeletedAtUtc.Value;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve()
    {
        ModerationStatus = BloggerStatus.Approved;
        IsVerified = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject()
    {
        ModerationStatus = BloggerStatus.Rejected;
        IsVerified = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RequestChanges()
    {
        ModerationStatus = BloggerStatus.NeedsChanges;
        IsVerified = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SubmitForModeration()
    {
        ModerationStatus = BloggerStatus.Pending;
        IsVerified = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
