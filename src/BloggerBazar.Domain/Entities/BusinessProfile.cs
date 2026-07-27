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
    public string? Description { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<Campaign> Campaigns { get; private set; } = [];
    public IReadOnlyCollection<Deal> Deals { get; private set; } = [];
    public IReadOnlyCollection<Review> Reviews { get; private set; } = [];
    public IReadOnlyCollection<CollaborationRequest> CollaborationRequests { get; private set; } = [];

    public static BusinessProfile Create(long telegramUserId, string name, string? city) => new(telegramUserId, name, city);

    public void Update(string name, string? username, string? city, string? logoUrl, string? description, string? phone, string? email)
    {
        Name = name;
        Username = username;
        City = city;
        LogoUrl = logoUrl;
        Description = description;
        Phone = phone;
        Email = email;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
