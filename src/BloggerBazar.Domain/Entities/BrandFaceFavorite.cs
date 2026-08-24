namespace BloggerBazar.Domain.Entities;

public sealed class BrandFaceFavorite
{
    private BrandFaceFavorite() { }

    private BrandFaceFavorite(Guid platformUserId, Guid brandFaceId)
    {
        Id = Guid.NewGuid();
        PlatformUserId = platformUserId;
        BrandFaceId = brandFaceId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PlatformUserId { get; private set; }
    public Guid BrandFaceId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static BrandFaceFavorite Create(Guid platformUserId, Guid brandFaceId) => new(platformUserId, brandFaceId);
}
