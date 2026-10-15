using BloggerBazar.Api.Contracts.Telegram;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.Users;
using BloggerBazar.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Api.Controllers;

[Route("api/webhooks/telegram")]
[EnableRateLimiting("telegram-webhook")]
// The bot's only webhook: /start, /phone and a shared contact. Contact-unlock payments were removed from the product.
public sealed class TelegramWebhookController(
    ISender mediator,
    IOptions<TelegramOptions> telegramOptions,
    ITelegramBotClient botClient,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] TelegramWebhookUpdate? update, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || update is null)
        {
            logger.LogWarning("Telegram webhook payload was rejected. TraceId {TraceId}; SourceIp {SourceIp}", HttpContext.TraceIdentifier, HttpContext.Connection.RemoteIpAddress?.ToString());
            return BadRequest();
        }

        if (update.Message is { } message && IsStartCommand(message.Text, telegramOptions.Value.BotUsername))
        {
            if (message.Chat is { } chat)
            {
                try
                {
                    await botClient.SendStartMessageAsync(chat.Id, StartPayload(message.Text), cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A non-2xx answer makes Telegram redeliver this update and hold the updates behind it;
                    // a missed greeting is not worth that, so log it and acknowledge.
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

    internal static string? StartPayload(string? text)
    {
        var parts = text?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts is { Length: 2 } ? parts[1] : null;
    }

    internal static bool IsStartCommand(string? text, string botUsername)
    {
        // A deep link (t.me/bot?start=payload) sends "/start payload"; the payload is the ad label (StartPayload).
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
