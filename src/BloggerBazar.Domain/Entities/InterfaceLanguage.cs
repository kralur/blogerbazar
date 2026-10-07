namespace BloggerBazar.Domain.Entities;

// Interface languages a user can choose; stored as lowercase codes on PlatformUser (D39).
public static class InterfaceLanguage
{
    public const string Russian = "ru";
    public const string Uzbek = "uz";

    public static bool IsSupported(string? language) => language is Russian or Uzbek;
}
