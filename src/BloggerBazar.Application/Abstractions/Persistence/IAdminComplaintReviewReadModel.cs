using BloggerBazar.Application.Features.Complaints;

namespace BloggerBazar.Application.Abstractions.Persistence;

// What the web admin needs next to one complaint to decide it (read only).
public interface IAdminComplaintReviewReadModel
{
    // For a deal or an offer: the participant who is not the reporter; null for other targets or a stranger's report.
    Task<long?> GetOtherSideAsync(string targetType, Guid targetId, long reporterTelegramUserId, CancellationToken cancellationToken);
    // The person's profiles and campaigns: what complaints about this person point at.
    Task<IReadOnlyList<Guid>> GetComplaintTargetIdsAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<AdminReviewSummaryDto> GetReviewsAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<AdminComplaintPersonDto?> GetPersonAsync(long telegramUserId, CancellationToken cancellationToken);
    // Deals and offers between the two people, newest first.
    Task<IReadOnlyList<AdminSharedHistoryDto>> GetSharedHistoryAsync(long firstTelegramUserId, long secondTelegramUserId, CancellationToken cancellationToken);
}
