using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Telegram;

// A bot message in both interface languages; the recipient's stored language picks one (D39).
public sealed record BotText(string Russian, string Uzbek)
{
    // Users who never reported a language get both versions.
    public string For(string? language) => language switch
    {
        InterfaceLanguage.Russian => Russian,
        InterfaceLanguage.Uzbek => Uzbek,
        _ => $"{Russian}\n\n{Uzbek}"
    };
}
