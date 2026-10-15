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
    public string? PreferredLanguage { get; private set; }
    // A phone number Telegram itself delivered to the bot from this user; never typed into a form (D41).
    public string? VerifiedPhone { get; private set; }
    public DateTime? PhoneVerifiedAtUtc { get; private set; }
    public bool IsBlocked { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public long? DeletedByTelegramUserId { get; private set; }
    // Bot tips (D54): the user can switch them off; the last number keeps the next tip new.
    public bool TipsDisabled { get; private set; }
    public int? LastTipNumber { get; private set; }
    public DateTime? LastTipSentAtUtc { get; private set; }
    // Analytics: the last app open (kept at most hourly) and the ad label of the link the account first came from.
    public DateTime? LastSeenAtUtc { get; private set; }
    public string? Source { get; private set; }
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
    public void SetPreferredLanguage(string language)
    {
        if (!InterfaceLanguage.IsSupported(language)) throw new ArgumentException("The interface language is not supported.", nameof(language));
        PreferredLanguage = language;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void VerifyPhone(string phone, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(phone)) throw new ArgumentException("A verified phone is required.", nameof(phone));
        VerifiedPhone = phone;
        PhoneVerifiedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void SetTipsDisabled(bool disabled) { TipsDisabled = disabled; UpdatedAtUtc = DateTime.UtcNow; }
    // An admin preview moves the admin's own rotation on; the daily send mark (LastTipSentAtUtc) stays untouched.
    public void SetLastTipNumber(int number) { LastTipNumber = number; UpdatedAtUtc = DateTime.UtcNow; }
    public void SetBlocked(bool isBlocked) { IsBlocked = isBlocked; UpdatedAtUtc = DateTime.UtcNow; }

    // At most one write an hour: "who came today / this week" does not need every request.
    public void MarkSeen(DateTime utcNow)
    {
        if (LastSeenAtUtc is { } last && utcNow - last < TimeSpan.FromHours(1)) return;
        LastSeenAtUtc = utcNow;
    }

    // Only the first label counts: it says where the person came from, later links do not change it.
    public void SetSourceOnce(string? source)
    {
        if (Source is null && IsValidSource(source)) Source = source;
    }

    public static bool IsValidSource(string? source) =>
        source is { Length: >= 1 and <= 32 } && source.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
    public void SoftDelete(long deletedByTelegramUserId) { IsDeleted = true; DeletedAtUtc = DateTime.UtcNow; DeletedByTelegramUserId = deletedByTelegramUserId; UpdatedAtUtc = DeletedAtUtc.Value; }
    public void RestoreForNewOnboarding(string firstName, string? username)
    {
        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedByTelegramUserId = null;
        SelectedMarketplaceRole = null;
        SyncTelegramIdentity(firstName, username);
    }
}
