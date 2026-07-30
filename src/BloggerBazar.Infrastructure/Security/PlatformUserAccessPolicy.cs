using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Infrastructure.Security;

internal sealed class PlatformUserAccessPolicy(BloggerBazarDbContext dbContext) : IPlatformUserAccessPolicy
{
    public void EnsureActive(long telegramUserId)
    {
        var user = dbContext.PlatformUsers.SingleOrDefault(candidate => candidate.TelegramUserId == telegramUserId);
        if (user is { IsBlocked: true } or { IsDeleted: true })
        {
            throw new UnauthorizedAccessException("This Telegram account is not allowed to access the marketplace.");
        }
    }
}
