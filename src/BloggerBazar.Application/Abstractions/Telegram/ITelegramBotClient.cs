namespace BloggerBazar.Application.Abstractions.Telegram;

public interface ITelegramBotClient
{
    Task SendStartMessageAsync(long chatId, CancellationToken cancellationToken);
    Task SendNotificationAsync(long chatId, string text, CancellationToken cancellationToken) => Task.CompletedTask;
}
