using BloggerBazar.Application.Abstractions.Telegram;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Notifications;

internal static class BestEffortTelegramNotification
{
    public static async Task SendAsync(ITelegramBotClient? botClient, ILogger? logger, long chatId, string text, CancellationToken cancellationToken)
    {
        if (botClient is null) return;
        try { await botClient.SendNotificationAsync(chatId, text, cancellationToken); }
        catch (Exception exception) { logger?.LogWarning(exception, "Telegram notification delivery failed for chat {ChatId}", chatId); }
    }
}
