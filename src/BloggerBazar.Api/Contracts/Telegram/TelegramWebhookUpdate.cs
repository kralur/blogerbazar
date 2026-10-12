using System.Text.Json.Serialization;

namespace BloggerBazar.Api.Contracts.Telegram;

// Only the parts of a Telegram update the bot reacts to: commands and a shared contact.
public sealed record TelegramWebhookUpdate(
    [property: JsonPropertyName("message")] TelegramWebhookMessage? Message);

public sealed record TelegramWebhookMessage(
    [property: JsonPropertyName("from")] TelegramWebhookUser? From,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("chat")] TelegramWebhookChat? Chat,
    [property: JsonPropertyName("contact")] TelegramWebhookContact? Contact = null);

public sealed record TelegramWebhookUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string? FirstName = null,
    [property: JsonPropertyName("username")] string? Username = null);

// A shared contact; user_id is set only when it belongs to a Telegram account.
public sealed record TelegramWebhookContact(
    [property: JsonPropertyName("phone_number")] string PhoneNumber,
    [property: JsonPropertyName("user_id")] long? UserId);

public sealed record TelegramWebhookChat([property: JsonPropertyName("id")] long Id);
