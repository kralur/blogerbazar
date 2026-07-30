using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class PlatformUser
{
    private PlatformUser() { }

    private PlatformUser(long telegramUserId, string firstName, string? username)
    {
        Id = Guid.NewGuid();
        TelegramUserId = telegramUserId;
        FirstName = firstName;
        Username = username;
        Role = PlatformRole.Member;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public long TelegramUserId { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string? Username { get; private set; }
    public PlatformRole Role { get; private set; }
    public MarketplaceRole? SelectedMarketplaceRole { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public long? DeletedByTelegramUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static PlatformUser Create(long telegramUserId, string firstName, string? username) => new(telegramUserId, firstName, username);

    public void SyncTelegramIdentity(string firstName, string? username)
    {
        FirstName = firstName;
        Username = username;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetRole(PlatformRole role) { Role = role; UpdatedAtUtc = DateTime.UtcNow; }
    public void SelectMarketplaceRole(MarketplaceRole role) { SelectedMarketplaceRole = role; UpdatedAtUtc = DateTime.UtcNow; }
    public void SetBlocked(bool isBlocked) { IsBlocked = isBlocked; UpdatedAtUtc = DateTime.UtcNow; }
    public void SoftDelete(long deletedByTelegramUserId) { IsDeleted = true; DeletedAtUtc = DateTime.UtcNow; DeletedByTelegramUserId = deletedByTelegramUserId; UpdatedAtUtc = DeletedAtUtc.Value; }
}
