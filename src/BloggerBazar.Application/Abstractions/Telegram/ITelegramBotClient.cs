namespace BloggerBazar.Application.Abstractions.Telegram;

public interface ITelegramBotClient
{
    Task SendStartMessageAsync(long chatId, CancellationToken cancellationToken);
}
