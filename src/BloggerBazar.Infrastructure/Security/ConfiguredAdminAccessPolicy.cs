using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Infrastructure.Security;

internal sealed class ConfiguredAdminAccessPolicy(BloggerBazarDbContext dbContext, IConfiguredOwners owners) : IAdminAccessPolicy
{
    public void EnsureAllowed(long telegramUserId)
    {
        // Audit 2026-10-22: Owner counts only while the id is in the deployment settings, so taking an id out of
        // Administration:TelegramUserIds ends that person's owner access at once (the role is also lowered at sign-in).
        var role = dbContext.PlatformUsers.Where(user => user.TelegramUserId == telegramUserId && !user.IsBlocked && !user.IsDeleted)
            .Select(user => (PlatformRole?)user.Role).SingleOrDefault();
        var allowed = role == PlatformRole.Admin || (role == PlatformRole.Owner && owners.IsConfiguredOwner(telegramUserId));
        if (!allowed)
        {
            throw new UnauthorizedAccessException("Administrator access is required.");
        }
    }
}
