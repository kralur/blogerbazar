using BloggerBazar.Api.Contracts.Telegram;
using BloggerBazar.Api.Security;
using BloggerBazar.Application.Abstractions.Payments;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Infrastructure.Payments;
using BloggerBazar.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Api.Controllers;

[Route("api/webhooks/telegram")]
[EnableRateLimiting("telegram-webhook")]
public sealed class TelegramPaymentsWebhookController(
    ISender sender,
    ITelegramPaymentGateway paymentGateway,
    IOptions<ClickTelegramPaymentOptions> paymentOptions,
    IOptions<TelegramOptions> telegramOptions,
    ITelegramBotClient botClient,
    ILogger<TelegramPaymentsWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] TelegramPaymentWebhookUpdate? update, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || update is null || !TelegramWebhookPayloadValidator.IsValid(update))
        {
            logger.LogWarning("Telegram webhook payload was rejected. TraceId {TraceId}; SourceIp {SourceIp}", HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString());
            return BadRequest();
        }

        if (update.PreCheckoutQuery is { } checkout)
        {
            var isAmountValid = paymentOptions.Value.TryGetAmountUzs(checkout.Currency, checkout.TotalAmount, out var amountUzs);
            var validation = isAmountValid
                ? await sender.Send(new ValidateContactUnlockCheckoutCommand(checkout.InvoicePayload, checkout.From.Id, amountUzs), cancellationToken)
                : TelegramCheckoutValidationDto.Rejected();

            await paymentGateway.AnswerPreCheckoutQueryAsync(checkout.Id, validation.IsApproved, validation.ErrorMessage, cancellationToken);
            return Ok();
        }

        if (update.Message is { } message && IsStartCommand(message.Text, telegramOptions.Value.BotUsername))
        {
            if (message.Chat is { } chat)
            {
                try
                {
                    await botClient.SendStartMessageAsync(chat.Id, cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    logger.LogError(exception, "Unable to send Telegram start message to chat {ChatId}.", chat.Id);
                    return StatusCode(StatusCodes.Status502BadGateway);
                }
            }

            return Ok();
        }

        if (update.Message?.SuccessfulPayment is { } payment && update.Message.From is { } payer)
        {
            if (!paymentOptions.Value.TryGetAmountUzs(payment.Currency, payment.TotalAmount, out var amountUzs))
            {
                logger.LogWarning("Ignored Telegram payment with an unexpected amount or currency.");
                return Ok();
            }

            await sender.Send(
                new ConfirmContactUnlockPaymentCommand(payment.InvoicePayload, payment.TelegramPaymentChargeId, amountUzs, payer.Id),
                cancellationToken);
        }

        return Ok();
    }

    private static bool IsStartCommand(string? text, string botUsername)
    {
        if (string.Equals(text, "/start", StringComparison.Ordinal))
        {
            return true;
        }

        var normalizedUsername = botUsername.Trim().TrimStart('@');
        return !string.IsNullOrWhiteSpace(normalizedUsername)
            && string.Equals(text, $"/start@{normalizedUsername}", StringComparison.OrdinalIgnoreCase);
    }
}
