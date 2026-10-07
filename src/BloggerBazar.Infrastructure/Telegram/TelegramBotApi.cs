namespace BloggerBazar.Infrastructure.Telegram;

internal static class TelegramBotApi
{
    // A bot token contains a colon ("123:ABC"). As a plain string "bot123:ABC/method" parses as an
    // absolute URI with the scheme "bot123", so the request never reaches Telegram. A rooted relative
    // URI keeps the colon in the path and resolves against the client's https://api.telegram.org/ base.
    public static Uri MethodUri(string botToken, string method) => new($"/bot{botToken}/{method}", UriKind.Relative);
}
