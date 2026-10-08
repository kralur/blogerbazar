using System.Text.Json.Serialization;

namespace BloggerBazar.Api.Contracts.Telegram;

public sealed record TelegramPaymentWebhookUpdate(
    [property: JsonPropertyName("pre_checkout_query")] TelegramPreCheckoutQuery? PreCheckoutQuery,
    [property: JsonPropertyName("message")] TelegramPaymentMessage? Message);

public sealed record TelegramPreCheckoutQuery(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] TelegramWebhookUser From,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("total_amount")] int TotalAmount,
    [property: JsonPropertyName("invoice_payload")] string InvoicePayload);

public sealed record TelegramPaymentMessage(
    [property: JsonPropertyName("from")] TelegramWebhookUser? From,
    [property: JsonPropertyName("successful_payment")] TelegramSuccessfulPayment? SuccessfulPayment,
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

public sealed record TelegramSuccessfulPayment(
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("total_amount")] int TotalAmount,
    [property: JsonPropertyName("invoice_payload")] string InvoicePayload,
    [property: JsonPropertyName("telegram_payment_charge_id")] string TelegramPaymentChargeId);
