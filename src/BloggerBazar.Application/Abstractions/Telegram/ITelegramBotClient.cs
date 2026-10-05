namespace BloggerBazar.Application.Abstractions.Telegram;

public interface ITelegramBotClient
{
    Task SendStartMessageAsync(long chatId, CancellationToken cancellationToken);
    Task SendNotificationAsync(long chatId, string text, CancellationToken cancellationToken) => Task.CompletedTask;

    // miniAppRoute is a hash route such as "/deal/{id}"; the message gets a button that opens it.
    Task SendNotificationAsync(long chatId, string text, string miniAppRoute, CancellationToken cancellationToken) =>
        SendNotificationAsync(chatId, text, cancellationToken);
}
