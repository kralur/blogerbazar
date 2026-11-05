namespace BloggerBazar.Application.Abstractions.Security;

// The platform owner's Telegram ids from deployment settings (Administration:TelegramUserIds).
public interface IConfiguredOwners
{
    bool IsConfiguredOwner(long telegramUserId);

    // Every configured owner, for messages meant only for them (error-spike alerts, D60).
    IReadOnlyList<long> All => [];
}
