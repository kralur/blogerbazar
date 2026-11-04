using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Security;

internal sealed class PlatformUserAccessPolicy(BloggerBazarDbContext dbContext) : IPlatformUserAccessPolicy
{
    public void EnsureActive(long telegramUserId)
    {
        // Runs on almost every request: one indexed EXISTS instead of loading and tracking the whole account row.
        var refused = dbContext.PlatformUsers.AsNoTracking()
            .Any(candidate => candidate.TelegramUserId == telegramUserId && (candidate.IsBlocked || candidate.IsDeleted));
        if (refused)
        {
            throw new UnauthorizedAccessException("This Telegram account is not allowed to access the marketplace.");
        }
    }
}
