namespace BloggerBazar.Infrastructure.Security;

public sealed class AdministrationOptions
{
    public const string SectionName = "Administration";
    public long[] TelegramUserIds { get; init; } = [];
}
