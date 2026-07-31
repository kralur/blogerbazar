namespace BloggerBazar.Domain.Entities;

public sealed class Favorite
{
    private Favorite() { }

    private Favorite(Guid platformUserId, Guid bloggerId)
    {
        Id = Guid.NewGuid();
        PlatformUserId = platformUserId;
        BloggerId = bloggerId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PlatformUserId { get; private set; }
    public Guid BloggerId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Favorite Create(Guid platformUserId, Guid bloggerId) => new(platformUserId, bloggerId);
}
