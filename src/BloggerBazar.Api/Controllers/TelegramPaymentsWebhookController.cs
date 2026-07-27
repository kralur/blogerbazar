using BloggerBazar.Api.Contracts.Telegram;
using BloggerBazar.Application.Abstractions.Payments;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Infrastructure.Payments;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/webhooks/telegram")]
[DisableRateLimiting]
public sealed class TelegramPaymentsWebhookController(
    ISender sender,
    ITelegramPaymentGateway paymentGateway,
    ITelegramWebhookValidator webhookValidator,
    IOptions<ClickTelegramPaymentOptions> paymentOptions,
    ITelegramBotClient botClient,
    ILogger<TelegramPaymentsWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(TelegramPaymentWebhookUpdate update, CancellationToken cancellationToken)
    {
        if (!webhookValidator.IsValid(Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString()))
        {
            return Unauthorized();
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

        if (update.Message?.Text is "/start" or "/start@BloggerBazarBot")
        {
            if (update.Message.Chat is { } chat)
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
}
