namespace BloggerBazar.Application.Abstractions.Security;

// The platform owner's Telegram ids from deployment settings (Administration:TelegramUserIds).
public interface IConfiguredOwners
{
    bool IsConfiguredOwner(long telegramUserId);
}
