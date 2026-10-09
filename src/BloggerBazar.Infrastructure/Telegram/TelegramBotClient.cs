using System.Net.Http.Json;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Telegram;

internal sealed class TelegramBotClient(HttpClient httpClient, IOptions<TelegramOptions> options, IRecipientLanguageLookup languages) : ITelegramBotClient
{
    internal static readonly BotText StartText = new(
        "👋 Добро пожаловать в BloggerBazar! Здесь бизнес находит блогеров для рекламы.\n\n" +
        "Здесь я буду присылать уведомления: новые отклики и предложения, решения по ним, статус сделок и напоминания об отзывах.\n\n" +
        "Нажмите «Открыть», чтобы начать.",
        "👋 BloggerBazar’ga xush kelibsiz! Bu yerda biznes reklama uchun blogerlarni topadi.\n\n" +
        "Bu yerda sizga bildirishnomalar yuboraman: yangi javoblar va takliflar, ular bo‘yicha qarorlar, bitimlar holati va fikr qoldirish haqida eslatmalar.\n\n" +
        "Boshlash uchun «Ochish» tugmasini bosing.");

    internal static readonly BotText StartButton = new("🚀 Открыть", "🚀 Ochish");
    internal static readonly BotText OpenButton = new("Открыть", "Ochish");

    internal static readonly BotText PhoneRequestText = new(
        "Нажмите кнопку ниже, чтобы поделиться номером из Telegram. Он будет в ваших профилях BloggerBazar.",
        "Telegram’dagi raqamingizni ulashish uchun quyidagi tugmani bosing. U BloggerBazar profillaringizda bo‘ladi.");
    internal static readonly BotText PhoneRejectedText = new(
        "Нужен ваш собственный номер. Нажмите кнопку ниже, а не отправляйте чужой контакт.",
        "O‘zingizning raqamingiz kerak. Boshqa kontaktni yubormang, quyidagi tugmani bosing.");
    internal static readonly BotText PhoneNotSavedText = new(
        "Не получилось сохранить этот номер. Если вы удаляли аккаунт, откройте приложение заново и попробуйте ещё раз.",
        "Bu raqamni saqlab bo‘lmadi. Agar akkauntni o‘chirgan bo‘lsangiz, ilovani qayta oching va yana urinib ko‘ring.");
    internal static readonly BotText ShareButton = new("📱 Поделиться номером", "📱 Raqamni ulashish");

    internal static BotText PhoneVerifiedText(string phone) => new(
        $"Номер {phone} подтверждён. Вернитесь в приложение. Сменить номер: команда /phone.",
        $"{phone} raqami tasdiqlandi. Ilovaga qayting. Raqamni o‘zgartirish: /phone buyrug‘i.");

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
        using var response = await httpClient.PostAsJsonAsync(TelegramBotApi.MethodUri(botToken, "sendMessage"), new
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

    public async Task SendPhoneRequestAsync(long chatId, bool rejectedContact, CancellationToken cancellationToken)
    {
        var language = await LanguageOfAsync(chatId, cancellationToken);
        await SendMessageAsync(new
        {
            chat_id = chatId,
            text = (rejectedContact ? PhoneRejectedText : PhoneRequestText).For(language),
            reply_markup = new
            {
                keyboard = new[] { new[] { new { text = ButtonLabel(ShareButton, language), request_contact = true } } },
                resize_keyboard = true,
                one_time_keyboard = true
            }
        }, cancellationToken);
    }

    public async Task SendPhoneVerifiedAsync(long chatId, string phone, CancellationToken cancellationToken)
    {
        var language = await LanguageOfAsync(chatId, cancellationToken);
        await SendMessageAsync(new { chat_id = chatId, text = PhoneVerifiedText(phone).For(language), reply_markup = new { remove_keyboard = true } }, cancellationToken);
    }

    public async Task SendPhoneNotSavedAsync(long chatId, CancellationToken cancellationToken)
    {
        var language = await LanguageOfAsync(chatId, cancellationToken);
        await SendMessageAsync(new { chat_id = chatId, text = PhoneNotSavedText.For(language), reply_markup = new { remove_keyboard = true } }, cancellationToken);
    }

    // The app name after the person's name, so the saved phone contact shows where it came from.
    public async Task SendContactAsync(long chatId, string phone, string name, CancellationToken cancellationToken)
    {
        var botToken = options.Value.BotToken;
        if (string.IsNullOrWhiteSpace(botToken)) throw new InvalidOperationException("Telegram:BotToken must be configured to send bot messages.");
        using var response = await httpClient.PostAsJsonAsync(TelegramBotApi.MethodUri(botToken, "sendContact"), new
        {
            chat_id = chatId,
            phone_number = phone,
            first_name = name.Length > 64 ? name[..64] : name,
            last_name = "(BloggerBazar)"
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task SendMessageAsync(object payload, CancellationToken cancellationToken)
    {
        var botToken = options.Value.BotToken;
        if (string.IsNullOrWhiteSpace(botToken)) throw new InvalidOperationException("Telegram:BotToken must be configured to send bot messages.");
        using var response = await httpClient.PostAsJsonAsync(TelegramBotApi.MethodUri(botToken, "sendMessage"), payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task SendNotificationAsync(long chatId, string text, CancellationToken cancellationToken) =>
        SendNotificationCoreAsync(chatId, text, null, null, OpenButton, cancellationToken);

    public Task SendNotificationAsync(long chatId, string text, string miniAppRoute, CancellationToken cancellationToken) =>
        SendNotificationCoreAsync(chatId, text, miniAppRoute, null, OpenButton, cancellationToken);

    public async Task SendNotificationAsync(long chatId, BotText text, string? miniAppRoute, CancellationToken cancellationToken)
    {
        var language = await LanguageOfAsync(chatId, cancellationToken);
        await SendNotificationCoreAsync(chatId, text.For(language), miniAppRoute, language, text.Button ?? OpenButton, cancellationToken);
    }

    private async Task<string?> LanguageOfAsync(long chatId, CancellationToken cancellationToken)
    {
        // A failed lookup must not cost the user the message: fall back to both languages.
        try { return await languages.GetAsync(chatId, cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return null; }
    }

    private async Task SendNotificationCoreAsync(long chatId, string text, string? miniAppRoute, string? language, BotText button, CancellationToken cancellationToken)
    {
        var telegram = options.Value;
        if (string.IsNullOrWhiteSpace(telegram.BotToken)) throw new InvalidOperationException("Telegram:BotToken must be configured to send bot messages.");
        var buttonUrl = MiniAppRouteUrl(telegram.MiniAppUrl, miniAppRoute);
        using var response = await httpClient.PostAsJsonAsync(TelegramBotApi.MethodUri(telegram.BotToken, "sendMessage"), new
        {
            chat_id = chatId,
            text,
            reply_markup = buttonUrl is null
                ? null
                : new { inline_keyboard = new[] { new[] { new { text = ButtonLabel(button, language), web_app = new { url = buttonUrl } } } } }
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
