using BloggerBazar.Api.Contracts.Telegram;
using BloggerBazar.Api.Security;
using BloggerBazar.Application.Abstractions.Payments;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Application.Features.Users;
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
    ISender mediator,
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
                ? await mediator.Send(new ValidateContactUnlockCheckoutCommand(checkout.InvoicePayload, checkout.From.Id, amountUzs), cancellationToken)
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
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A non-2xx answer makes Telegram redeliver this update and hold the updates behind it
                    // (payments included); a missed greeting is not worth that, so log it and acknowledge.
                    logger.LogError(exception, "Unable to send Telegram start message to chat {ChatId}.", chat.Id);
                }
            }

            return Ok();
        }

        if (update.Message is { Chat: { } phoneChat } && IsCommand(update.Message.Text, "/phone", telegramOptions.Value.BotUsername))
        {
            await BestEffortAsync(() => botClient.SendPhoneRequestAsync(phoneChat.Id, false, cancellationToken), phoneChat.Id);
            return Ok();
        }

        if (update.Message is { Contact: { } contact, From: { } contactOwner, Chat: { } contactChat })
        {
            // Only a contact the sender shared about themselves proves the number; a forwarded card proves nothing.
            var ownContact = contact.UserId == contactOwner.Id;
            var verifiedPhone = ownContact
                ? await mediator.Send(new VerifyTelegramPhoneCommand(contactOwner.Id, contactOwner.FirstName ?? "", contactOwner.Username, contact.PhoneNumber), cancellationToken)
                : null;
            await BestEffortAsync(() => verifiedPhone is not null
                ? botClient.SendPhoneVerifiedAsync(contactChat.Id, verifiedPhone, cancellationToken)
                : ownContact
                    ? botClient.SendPhoneNotSavedAsync(contactChat.Id, cancellationToken)
                    : botClient.SendPhoneRequestAsync(contactChat.Id, true, cancellationToken), contactChat.Id);
            return Ok();
        }

        if (update.Message?.SuccessfulPayment is { } payment && update.Message.From is { } payer)
        {
            if (!paymentOptions.Value.TryGetAmountUzs(payment.Currency, payment.TotalAmount, out var amountUzs))
            {
                logger.LogWarning("Ignored Telegram payment with an unexpected amount or currency.");
                return Ok();
            }

            await mediator.Send(
                new ConfirmContactUnlockPaymentCommand(payment.InvoicePayload, payment.TelegramPaymentChargeId, amountUzs, payer.Id),
                cancellationToken);
        }

        return Ok();
    }

    // A failed bot reply must not make Telegram redeliver the update and hold the ones behind it.
    private async Task BestEffortAsync(Func<Task> send, long chatId)
    {
        try { await send(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to send a Telegram reply to chat {ChatId}.", chatId);
        }
    }

    internal static bool IsCommand(string? text, string command, string botUsername)
    {
        var first = text?.Split(' ', 2)[0];
        if (string.Equals(first, command, StringComparison.Ordinal)) return true;
        var normalizedUsername = botUsername.Trim().TrimStart('@');
        return !string.IsNullOrWhiteSpace(normalizedUsername)
            && string.Equals(first, $"{command}@{normalizedUsername}", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsStartCommand(string? text, string botUsername)
    {
        // A deep link (t.me/bot?start=payload) sends "/start payload"; the payload is not used yet.
        var command = text?.Split(' ', 2)[0];
        if (string.Equals(command, "/start", StringComparison.Ordinal))
        {
            return true;
        }

        var normalizedUsername = botUsername.Trim().TrimStart('@');
        return !string.IsNullOrWhiteSpace(normalizedUsername)
            && string.Equals(command, $"/start@{normalizedUsername}", StringComparison.OrdinalIgnoreCase);
    }
}
