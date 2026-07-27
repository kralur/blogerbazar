namespace BloggerBazar.Application.Abstractions.Security;

public interface ITelegramWebAppValidator
{
    TelegramWebAppUser Validate(string initData);
}

public sealed record TelegramWebAppUser(long Id, string? Username, string FirstName);
