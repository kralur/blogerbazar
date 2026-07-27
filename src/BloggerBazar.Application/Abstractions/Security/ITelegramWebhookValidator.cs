namespace BloggerBazar.Application.Abstractions.Security;

public interface ITelegramWebhookValidator
{
    bool IsValid(string? secretToken);
}
