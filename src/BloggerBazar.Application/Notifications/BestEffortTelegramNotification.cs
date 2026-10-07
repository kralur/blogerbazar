using BloggerBazar.Application.Abstractions.Telegram;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Notifications;

internal static class BestEffortTelegramNotification
{
    public static Task SendAsync(ITelegramBotClient? botClient, ILogger? logger, long chatId, BotText text, CancellationToken cancellationToken) =>
        SendCoreAsync(botClient, logger, chatId, text, null, cancellationToken);

    public static Task SendAsync(ITelegramBotClient? botClient, ILogger? logger, long chatId, BotText text, string miniAppRoute, CancellationToken cancellationToken) =>
        SendCoreAsync(botClient, logger, chatId, text, miniAppRoute, cancellationToken);

    private static async Task SendCoreAsync(ITelegramBotClient? botClient, ILogger? logger, long chatId, BotText text, string? miniAppRoute, CancellationToken cancellationToken)
    {
        if (botClient is null) return;
        try { await botClient.SendNotificationAsync(chatId, text, miniAppRoute, cancellationToken); }
        catch (Exception exception) { logger?.LogWarning(exception, "Telegram notification delivery failed for chat {ChatId}", chatId); }
    }
}
