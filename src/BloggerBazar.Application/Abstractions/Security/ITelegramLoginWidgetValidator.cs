namespace BloggerBazar.Application.Abstractions.Security;

// Telegram Login Widget data for the web admin. It is signed differently from Mini App initData
// (key = SHA256(bot token)), so it has its own validator.
public interface ITelegramLoginWidgetValidator
{
    TelegramLoginWidgetUser Validate(IReadOnlyDictionary<string, string> fields);
}

// Hash is the verified signature (lowercase hex): the same signed data must not open a second session.
public sealed record TelegramLoginWidgetUser(long Id, string FirstName, string? Username, string Hash);
