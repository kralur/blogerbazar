using BloggerBazar.Application.Abstractions.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Security;

internal sealed class ConfiguredAdminAccessPolicy(IOptions<AdministrationOptions> options) : IAdminAccessPolicy
{
    public void EnsureAllowed(long telegramUserId)
    {
        if (!options.Value.TelegramUserIds.Contains(telegramUserId))
        {
            throw new UnauthorizedAccessException("Administrator access is required.");
        }
    }
}
