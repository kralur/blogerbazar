using BloggerBazar.Api.Contracts.Telegram;

namespace BloggerBazar.Api.Security;

internal static class TelegramWebhookPayloadValidator
{
    public static bool IsValid(TelegramPaymentWebhookUpdate update)
    {
        if (update.PreCheckoutQuery is { } checkout)
        {
            return !string.IsNullOrWhiteSpace(checkout.Id)
                && checkout.From is { Id: > 0 }
                && !string.IsNullOrWhiteSpace(checkout.Currency)
                && checkout.TotalAmount > 0
                && !string.IsNullOrWhiteSpace(checkout.InvoicePayload);
        }

        if (update.Message?.SuccessfulPayment is { } payment)
        {
            return update.Message.From is { Id: > 0 }
                && !string.IsNullOrWhiteSpace(payment.Currency)
                && payment.TotalAmount > 0
                && !string.IsNullOrWhiteSpace(payment.InvoicePayload)
                && !string.IsNullOrWhiteSpace(payment.TelegramPaymentChargeId);
        }

        return true;
    }
}
