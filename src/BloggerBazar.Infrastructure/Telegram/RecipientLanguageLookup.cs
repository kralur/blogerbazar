using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Telegram;

// Bot chats are private, so the chat id is the recipient's Telegram user id.
internal interface IRecipientLanguageLookup
{
    Task<string?> GetAsync(long chatId, CancellationToken cancellationToken);
}

internal sealed class RecipientLanguageLookup(BloggerBazarDbContext dbContext) : IRecipientLanguageLookup
{
    public Task<string?> GetAsync(long chatId, CancellationToken cancellationToken) =>
        dbContext.PlatformUsers.AsNoTracking()
            .Where(user => user.TelegramUserId == chatId)
            .Select(user => user.PreferredLanguage)
            .FirstOrDefaultAsync(cancellationToken);
}
