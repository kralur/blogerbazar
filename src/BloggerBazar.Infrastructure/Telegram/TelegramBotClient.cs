using System.Net.Http.Json;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Telegram;

internal sealed class TelegramBotClient(HttpClient httpClient, IOptions<TelegramOptions> options) : ITelegramBotClient
{
    // The backend does not store the user's interface language, so the greeting carries Russian and Uzbek.
    internal const string StartMessage =
        "👋 Добро пожаловать в BloggerBazar — площадку, где бизнес находит блогеров для рекламы.\n\n" +
        "Здесь я буду присылать уведомления: новые отклики и предложения, решения по ним, статус сделок и напоминания об отзывах.\n\n" +
        "Нажмите «Открыть», чтобы начать.\n\n" +
        "👋 BloggerBazar’ga xush kelibsiz — bu yerda biznes reklama uchun blogerlarni topadi.\n\n" +
        "Bu yerda sizga bildirishnomalar yuboraman: yangi javoblar va takliflar, ular bo‘yicha qarorlar, bitimlar holati va fikr qoldirish haqida eslatmalar.\n\n" +
        "Boshlash uchun «Ochish» tugmasini bosing.";

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
            text = StartMessage,
            reply_markup = Uri.TryCreate(telegram.MiniAppUrl, UriKind.Absolute, out var miniAppUrl)
                ? new
                {
                    inline_keyboard = new[]
                    {
                        new[] { new { text = "🚀 Открыть / Ochish", web_app = new { url = miniAppUrl.ToString() } }
                    }
                    }
                }
                : null
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task SendNotificationAsync(long chatId, string text, CancellationToken cancellationToken) =>
        SendNotificationCoreAsync(chatId, text, null, cancellationToken);

    public Task SendNotificationAsync(long chatId, string text, string miniAppRoute, CancellationToken cancellationToken) =>
        SendNotificationCoreAsync(chatId, text, miniAppRoute, cancellationToken);

    private async Task SendNotificationCoreAsync(long chatId, string text, string? miniAppRoute, CancellationToken cancellationToken)
    {
        var telegram = options.Value;
        if (string.IsNullOrWhiteSpace(telegram.BotToken)) throw new InvalidOperationException("Telegram:BotToken must be configured to send bot messages.");
        var buttonUrl = MiniAppRouteUrl(telegram.MiniAppUrl, miniAppRoute);
        using var response = await httpClient.PostAsJsonAsync($"bot{telegram.BotToken}/sendMessage", new
        {
            chat_id = chatId,
            text,
            reply_markup = buttonUrl is null
                ? null
                : new { inline_keyboard = new[] { new[] { new { text = "Открыть / Ochish", web_app = new { url = buttonUrl } } } } }
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    internal static string? MiniAppRouteUrl(string miniAppUrl, string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || !Uri.TryCreate(miniAppUrl, UriKind.Absolute, out var baseUrl) || baseUrl.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        // The route travels as a query parameter: Telegram appends its launch data to the URL fragment.
        var url = new UriBuilder(baseUrl) { Fragment = string.Empty };
        var routeParameter = $"bb_route={Uri.EscapeDataString(route)}";
        url.Query = string.IsNullOrEmpty(url.Query) ? routeParameter : $"{url.Query.TrimStart('?')}&{routeParameter}";
        return url.Uri.AbsoluteUri;
    }
}
