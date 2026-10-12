using BloggerBazar.Application.Features.Tips;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IBotTipRepository
{
    // Active users with tips on who joined before today and got no tip since dayStartUtc.
    Task<IReadOnlyList<BotTipRecipient>> GetDueAsync(DateTime dayStartUtc, int take, CancellationToken cancellationToken);

    // Marks today's tip atomically; false when this user already got one today (also from another app instance).
    Task<bool> TryClaimAsync(long telegramUserId, int? tipNumber, DateTime dayStartUtc, DateTime nowUtc, CancellationToken cancellationToken);
}
