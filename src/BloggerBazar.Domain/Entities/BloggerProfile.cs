using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class BloggerProfile
{
    private BloggerProfile() { }

    private BloggerProfile(long telegramUserId, string name, string city, IReadOnlyCollection<string> categories)
    {
        Id = Guid.NewGuid();
        TelegramUserId = telegramUserId;
        Name = name;
        City = city;
        Categories = categories.ToList();
        Status = BloggerStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public long TelegramUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? LastName { get; private set; }
    public string? Username { get; private set; }
    public string City { get; private set; } = null!;
    public IReadOnlyCollection<string> Categories { get; private set; } = [];
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? CoverUrl { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public int? Age { get; private set; }
    public string? Gender { get; private set; }
    public string? Language { get; private set; }
    public string? Subcategory { get; private set; }
    public int? PriceFrom { get; private set; }
    public int? PriceTo { get; private set; }
    public string? PriceNote { get; private set; }
    public int TotalFollowers { get; private set; }
    public int? AverageReach { get; private set; }
    public decimal? EngagementRate { get; private set; }
    public int? StoriesPrice { get; private set; }
    public int? ReelsPrice { get; private set; }
    public int? PostPrice { get; private set; }
    public int? IntegrationPrice { get; private set; }
    public bool BarterEnabled { get; private set; }
    public bool IsVerified { get; private set; }
    public bool IsPromoted { get; private set; }
    public BloggerStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<CampaignApplication> CampaignApplications { get; private set; } = [];
    public IReadOnlyCollection<Deal> Deals { get; private set; } = [];
    public IReadOnlyCollection<Review> Reviews { get; private set; } = [];
    public IReadOnlyCollection<PortfolioItem> PortfolioItems { get; private set; } = [];
    public IReadOnlyCollection<SocialPlatform> Platforms { get; private set; } = [];
    public IReadOnlyCollection<CollaborationRequest> IncomingRequests { get; private set; } = [];

    public static BloggerProfile Create(long telegramUserId, string name, string city, IReadOnlyCollection<string> categories) =>
        new(telegramUserId, name, city, categories);

    public void UpdatePublicProfile(
        string name,
        string? lastName,
        string? username,
        string city,
        IReadOnlyCollection<string> categories,
        string? bio,
        string? avatarUrl,
        string? phone,
        string? email,
        int totalFollowers,
        int? averageReach,
        decimal? engagementRate,
        int? storiesPrice,
        int? reelsPrice,
        int? postPrice,
        int? integrationPrice,
        bool barterEnabled)
    {
        Name = name;
        LastName = lastName;
        Username = username;
        City = city;
        Categories = categories.ToList();
        Bio = bio;
        AvatarUrl = avatarUrl;
        Phone = phone;
        Email = email;
        TotalFollowers = totalFollowers;
        AverageReach = averageReach;
        EngagementRate = engagementRate;
        StoriesPrice = storiesPrice;
        ReelsPrice = reelsPrice;
        PostPrice = postPrice;
        IntegrationPrice = integrationPrice;
        BarterEnabled = barterEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateExtendedProfile(
        string? coverUrl,
        int? age,
        string? gender,
        string? language,
        string? subcategory,
        int? priceFrom,
        int? priceTo,
        string? priceNote)
    {
        CoverUrl = coverUrl;
        Age = age;
        Gender = gender;
        Language = language;
        Subcategory = subcategory;
        PriceFrom = priceFrom;
        PriceTo = priceTo;
        PriceNote = priceNote;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve()
    {
        Status = BloggerStatus.Approved;
        IsVerified = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject()
    {
        if (Status != BloggerStatus.Pending)
        {
            throw new InvalidOperationException("Only pending blogger profiles can be rejected.");
        }

        Status = BloggerStatus.Rejected;
        IsVerified = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
