using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Application.Abstractions.Payments;
using BloggerBazar.Application.Exceptions;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Payments;

internal sealed class TelegramPaymentGateway(
    HttpClient httpClient,
    IOptions<TelegramOptions> telegramOptions,
    IOptions<ClickTelegramPaymentOptions> paymentOptions) : ITelegramPaymentGateway
{
    public async Task<string> CreateInvoiceLinkAsync(TelegramInvoiceRequest request, CancellationToken cancellationToken)
    {
        var click = paymentOptions.Value;
        if (string.IsNullOrWhiteSpace(click.TelegramProviderToken))
        {
            throw new PaymentProviderUnavailableException("Payment provider is not configured.");
        }

        var response = await PostAsync<TelegramApiResult<string>>(
            "createInvoiceLink",
            new
            {
                title = request.Title,
                description = request.Description,
                payload = request.Payload,
                provider_token = click.TelegramProviderToken,
                currency = click.Currency,
                prices = new[] { new { label = request.Title, amount = click.ToMinorUnits(request.AmountUzs) } }
            },
            cancellationToken);

        if (!response.Ok)
        {
            if (response.Description?.Contains("PAYMENT_PROVIDER_INVALID", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new PaymentProviderUnavailableException("Payment provider is not active for this bot.");
            }
            throw new InvalidOperationException("Telegram did not create an invoice link.");
        }
        if (string.IsNullOrWhiteSpace(response.Result)) throw new InvalidOperationException("Telegram did not create an invoice link.");

        return response.Result;
    }

    public async Task AnswerPreCheckoutQueryAsync(string queryId, bool isApproved, string? errorMessage, CancellationToken cancellationToken)
    {
        var response = await PostAsync<TelegramApiResult<bool>>(
            "answerPreCheckoutQuery",
            new
            {
                pre_checkout_query_id = queryId,
                ok = isApproved,
                error_message = isApproved ? null : errorMessage
            },
            cancellationToken);

        if (!response.Ok || !response.Result)
        {
            throw new InvalidOperationException("Telegram did not accept the checkout response.");
        }
    }

    private async Task<T> PostAsync<T>(string method, object body, CancellationToken cancellationToken)
    {
        var botToken = telegramOptions.Value.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException("Telegram:BotToken is not configured.");
        }

        using var response = await httpClient.PostAsJsonAsync($"bot{botToken}/{method}", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Telegram Payments API request failed.");
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Telegram Payments API returned an invalid response.");
    }

    private sealed record TelegramApiResult<T>(bool Ok, T? Result, string? Description);
}
