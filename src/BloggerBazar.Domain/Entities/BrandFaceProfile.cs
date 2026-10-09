namespace BloggerBazar.Domain.Entities;

public sealed class BrandFaceProfile
{
    private BrandFaceProfile() { }
    private BrandFaceProfile(long telegramUserId, string name, string city, IReadOnlyCollection<string> categories)
    {
        Id = Guid.NewGuid(); TelegramUserId = telegramUserId; Name = name; City = city; Categories = categories.ToArray(); CreatedAtUtc = DateTime.UtcNow; UpdatedAtUtc = CreatedAtUtc;
    }
    public Guid Id { get; private set; }
    public long TelegramUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public int? Age { get; private set; }
    public string? Gender { get; private set; }
    public IReadOnlyCollection<string> Languages { get; private set; } = [];
    public IReadOnlyCollection<string> Categories { get; private set; } = [];
    public string? Experience { get; private set; }
    public string? Instagram { get; private set; }
    public string? Telegram { get; private set; }
    public string? PortfolioUrl { get; private set; }
    public int? CollaborationPrice { get; private set; }
    public string? Description { get; private set; }
    public string? AvatarUrl { get; private set; }
    // What the brand face offers: fixed camelCase values (see BrandFaceFormats) (QA Q20).
    public IReadOnlyCollection<string> Formats { get; private set; } = [];
    public string? ShowreelUrl { get; private set; }
    // Extra photos besides the avatar, in upload order; at most MaxPhotos.
    public IReadOnlyCollection<string> PhotoUrls { get; private set; } = [];
    public const int MaxPhotos = 4;
    public bool IsPromoted { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public static BrandFaceProfile Create(long telegramUserId, string name, string city, IReadOnlyCollection<string> categories) => new(telegramUserId, name, city, categories);
    public void Update(string name, string city, int? age, string? gender, IReadOnlyCollection<string> languages, IReadOnlyCollection<string> categories, string? experience, string? instagram, string? telegram, string? portfolioUrl, int? collaborationPrice, string? description, string? avatarUrl)
    { Name = name; City = city; Age = age; Gender = gender; Languages = languages.ToArray(); Categories = categories.ToArray(); Experience = experience; Instagram = instagram; Telegram = telegram; PortfolioUrl = portfolioUrl; CollaborationPrice = collaborationPrice; Description = description; AvatarUrl = avatarUrl; UpdatedAtUtc = DateTime.UtcNow; }
    public void SetPresentation(IReadOnlyCollection<string> formats, string? showreelUrl)
    { Formats = formats.Distinct().ToArray(); ShowreelUrl = showreelUrl; UpdatedAtUtc = DateTime.UtcNow; }
    public bool CanAddPhoto => PhotoUrls.Count < MaxPhotos;
    public void AddPhoto(string url)
    {
        if (!CanAddPhoto) throw new InvalidOperationException("The photo limit is reached.");
        PhotoUrls = [.. PhotoUrls, url]; UpdatedAtUtc = DateTime.UtcNow;
    }
    public bool RemovePhoto(string url)
    {
        if (!PhotoUrls.Contains(url)) return false;
        PhotoUrls = PhotoUrls.Where(item => item != url).ToArray(); UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }
    public void SetPrimaryImageUrl(string? avatarUrl)
    { AvatarUrl = avatarUrl; UpdatedAtUtc = DateTime.UtcNow; }
    public void SoftDelete()
    { IsDeleted = true; IsPromoted = false; DeletedAtUtc = DateTime.UtcNow; UpdatedAtUtc = DeletedAtUtc.Value; }
    public void Restore()
    { IsDeleted = false; DeletedAtUtc = null; UpdatedAtUtc = DateTime.UtcNow; }
    public void SetPromotion(bool isPromoted) { IsPromoted = isPromoted; UpdatedAtUtc = DateTime.UtcNow; }
}
