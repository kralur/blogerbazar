using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IDealReminderRepository
{
    // Active deals at least 7 days old and completed deals still inside the review window, both sides not deleted.
    Task<IReadOnlyList<DealReminderCandidate>> GetCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken);

    // Records the reminder atomically; false when it was already sent (also by another app instance).
    Task<bool> TryClaimAsync(Guid dealId, DealReminderKind kind, MarketplaceRole recipientRole, DateTime nowUtc, CancellationToken cancellationToken);
}
