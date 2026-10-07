using System.Text.Json;
using BloggerBazar.Api.Controllers;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Infrastructure.Security;
using BloggerBazar.Infrastructure.Telegram;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Application.Tests.Infrastructure;

public sealed class TelegramBotClientTests
{
    [Theory]
    [InlineData("https://app.example/", "/offer/1", "https://app.example/?bb_route=%2Foffer%2F1")]
    [InlineData("https://app.example/mini?mode=tg#/home", "/deal/2", "https://app.example/mini?mode=tg&bb_route=%2Fdeal%2F2")]
    public void Mini_app_route_is_passed_as_a_query_parameter(string miniAppUrl, string route, string expected) =>
        Assert.Equal(expected, TelegramBotClient.MiniAppRouteUrl(miniAppUrl, route));

    [Theory]
    [InlineData("", "/offer/1")]
    [InlineData("http://app.example/", "/offer/1")]
    [InlineData("https://app.example/", null)]
    public void No_button_without_a_valid_https_mini_app_url_or_route(string miniAppUrl, string? route) =>
        Assert.Null(TelegramBotClient.MiniAppRouteUrl(miniAppUrl, route));

    [Fact]
    public void Start_message_greets_in_russian_and_uzbek()
    {
        Assert.Contains("Добро пожаловать в BloggerBazar", TelegramBotClient.StartText.Russian);
        Assert.Contains("BloggerBazar’ga xush kelibsiz", TelegramBotClient.StartText.Uzbek);
    }

    [Theory]
    [InlineData("ru", "Привет")]
    [InlineData("uz", "Salom")]
    [InlineData(null, "Привет\n\nSalom")]
    [InlineData("en", "Привет\n\nSalom")]
    public void Bot_text_uses_the_stored_language_or_both(string? language, string expected) =>
        Assert.Equal(expected, new BotText("Привет", "Salom").For(language));

    [Theory]
    [InlineData("ru", "Открыть")]
    [InlineData("uz", "Ochish")]
    [InlineData(null, "Открыть / Ochish")]
    public void Button_label_follows_the_language(string? language, string expected) =>
        Assert.Equal(expected, TelegramBotClient.ButtonLabel(TelegramBotClient.OpenButton, language));

    [Theory]
    [InlineData("uz", "Salom", "Ochish")]
    [InlineData(null, "Привет\n\nSalom", "Открыть / Ochish")]
    public async Task Notification_is_sent_in_the_recipient_language(string? language, string expectedText, string expectedButton)
    {
        var handler = new CapturingHandler();
        var client = new TelegramBotClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.org/") },
            Options.Create(new TelegramOptions { BotToken = "test-token", MiniAppUrl = "https://app.example/" }),
            new FixedLanguage(language));

        await client.SendNotificationAsync(7, new BotText("Привет", "Salom"), "/deal/1", CancellationToken.None);

        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(7, body.RootElement.GetProperty("chat_id").GetInt64());
        Assert.Equal(expectedText, body.RootElement.GetProperty("text").GetString());
        Assert.Equal(expectedButton, body.RootElement.GetProperty("reply_markup").GetProperty("inline_keyboard")[0][0].GetProperty("text").GetString());
    }

    private sealed class FixedLanguage(string? language) : IRecipientLanguageLookup
    {
        public Task<string?> GetAsync(long chatId, CancellationToken cancellationToken) => Task.FromResult(language);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }

    [Theory]
    [InlineData("/start", true)]
    [InlineData("/start campaign_42", true)]
    [InlineData("/start@BloggerBazarBot", true)]
    [InlineData("/start@bloggerbazarbot ref", true)]
    [InlineData("/started", false)]
    [InlineData("hello", false)]
    [InlineData(null, false)]
    public void Start_command_accepts_a_deep_link_payload(string? text, bool expected) =>
        Assert.Equal(expected, TelegramPaymentsWebhookController.IsStartCommand(text, "@BloggerBazarBot"));
}
