using BloggerBazar.Application.Abstractions.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Security;

internal sealed class ConfiguredOwners(IOptions<AdministrationOptions> options) : IConfiguredOwners
{
    public bool IsConfiguredOwner(long telegramUserId) => telegramUserId > 0 && options.Value.TelegramUserIds.Contains(telegramUserId);

    public IReadOnlyList<long> All => options.Value.TelegramUserIds.Where(id => id > 0).Distinct().ToArray();
}
