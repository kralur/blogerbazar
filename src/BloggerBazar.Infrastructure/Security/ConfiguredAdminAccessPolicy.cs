using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Infrastructure.Security;

internal sealed class ConfiguredAdminAccessPolicy(BloggerBazarDbContext dbContext) : IAdminAccessPolicy
{
    public void EnsureAllowed(long telegramUserId)
    {
        var allowed = dbContext.PlatformUsers.Any(user => user.TelegramUserId == telegramUserId
            && !user.IsBlocked
            && !user.IsDeleted
            && (user.Role == PlatformRole.Owner || user.Role == PlatformRole.Admin));
        if (!allowed)
        {
            throw new UnauthorizedAccessException("Administrator access is required.");
        }
    }
}
