using System.Net.Http.Json;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Telegram;

internal sealed class TelegramBotClient(HttpClient httpClient, IOptions<TelegramOptions> options) : ITelegramBotClient
{
    public async Task SendStartMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        var telegram = options.Value;
        var botToken = telegram.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException("Telegram:BotToken must be configured to send bot messages.");
        }

        using var response = await httpClient.PostAsJsonAsync($"bot{botToken}/sendMessage", new
        {
            chat_id = chatId,
            text = "Откройте Mini App, чтобы найти блогера или создать анкету.",
            reply_markup = Uri.TryCreate(telegram.MiniAppUrl, UriKind.Absolute, out var miniAppUrl)
                ? new
                {
                    inline_keyboard = new[]
                    {
                        new[] { new { text = "Открыть BloggerBazar", web_app = new { url = miniAppUrl.ToString() } }
                    }
                    }
                }
                : null
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
