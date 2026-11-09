namespace BloggerBazar.Application.Abstractions.Security;

public interface ITelegramWebAppValidator
{
    TelegramWebAppUser Validate(string initData);
}

// LaunchChatType: Telegram's signed chat_type, present when the Mini App was opened from a direct link in a chat
// ("sender" for the person's own chat, "private", "group", "supergroup" or "channel" for others).
public sealed record TelegramWebAppUser(long Id, string? Username, string FirstName, string? LaunchChatType = null);
