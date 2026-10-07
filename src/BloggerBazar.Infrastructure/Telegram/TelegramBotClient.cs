using System.Net.Http.Json;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Telegram;

internal sealed class TelegramBotClient(HttpClient httpClient, IOptions<TelegramOptions> options, IRecipientLanguageLookup languages) : ITelegramBotClient
{
    internal static readonly BotText StartText = new(
        "👋 Добро пожаловать в BloggerBazar — площадку, где бизнес находит блогеров для рекламы.\n\n" +
        "Здесь я буду присылать уведомления: новые отклики и предложения, решения по ним, статус сделок и напоминания об отзывах.\n\n" +
        "Нажмите «Открыть», чтобы начать.",
        "👋 BloggerBazar’ga xush kelibsiz — bu yerda biznes reklama uchun blogerlarni topadi.\n\n" +
        "Bu yerda sizga bildirishnomalar yuboraman: yangi javoblar va takliflar, ular bo‘yicha qarorlar, bitimlar holati va fikr qoldirish haqida eslatmalar.\n\n" +
        "Boshlash uchun «Ochish» tugmasini bosing.");

    internal static readonly BotText StartButton = new("🚀 Открыть", "🚀 Ochish");
    internal static readonly BotText OpenButton = new("Открыть", "Ochish");

    // Button labels are short, so the bilingual fallback joins them on one line.
    internal static string ButtonLabel(BotText label, string? language) =>
        language is null ? $"{label.Russian} / {label.Uzbek}" : label.For(language);

    public async Task SendStartMessageAsync(long chatId, CancellationToken cancellationToken)
    {
        var telegram = options.Value;
        var botToken = telegram.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException("Telegram:BotToken must be configured to send bot messages.");
        }

        var language = await LanguageOfAsync(chatId, cancellationToken);
        using var response = await httpClient.PostAsJsonAsync($"bot{botToken}/sendMessage", new
        {
            chat_id = chatId,
            text = StartText.For(language),
            reply_markup = Uri.TryCreate(telegram.MiniAppUrl, UriKind.Absolute, out var miniAppUrl)
                ? new
                {
                    inline_keyboard = new[]
                    {
                        new[] { new { text = ButtonLabel(StartButton, language), web_app = new { url = miniAppUrl.ToString() } }
                    }
                    }
                }
                : null
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task SendNotificationAsync(long chatId, string text, CancellationToken cancellationToken) =>
        SendNotificationCoreAsync(chatId, text, null, null, cancellationToken);

    public Task SendNotificationAsync(long chatId, string text, string miniAppRoute, CancellationToken cancellationToken) =>
        SendNotificationCoreAsync(chatId, text, miniAppRoute, null, cancellationToken);

    public async Task SendNotificationAsync(long chatId, BotText text, string? miniAppRoute, CancellationToken cancellationToken)
    {
        var language = await LanguageOfAsync(chatId, cancellationToken);
        await SendNotificationCoreAsync(chatId, text.For(language), miniAppRoute, language, cancellationToken);
    }

    private async Task<string?> LanguageOfAsync(long chatId, CancellationToken cancellationToken)
    {
        // A failed lookup must not cost the user the message: fall back to both languages.
        try { return await languages.GetAsync(chatId, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return null; }
    }

    private async Task SendNotificationCoreAsync(long chatId, string text, string? miniAppRoute, string? language, CancellationToken cancellationToken)
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
                : new { inline_keyboard = new[] { new[] { new { text = ButtonLabel(OpenButton, language), web_app = new { url = buttonUrl } } } } }
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
