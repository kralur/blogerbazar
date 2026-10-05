using BloggerBazar.Infrastructure.Telegram;

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
}
