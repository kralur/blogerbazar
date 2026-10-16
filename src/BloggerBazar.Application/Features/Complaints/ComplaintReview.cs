using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Admin;
using MediatR;

namespace BloggerBazar.Application.Features.Complaints;

// One complaint with everything an admin weighs before deciding: who is accused (their deals, reviews and other
// complaints), who reported (how often they report and how often that was dismissed) and what the two did together.
// Read only; Owner/Admin. The accused is the owner of the reported profile or campaign, or for a deal or an offer the
// participant who is not the reporter.
public sealed record AdminReviewItemDto(int Rating, string? Comment, string? ReviewerName, DateTime CreatedAtUtc, bool IsPublished);
public sealed record AdminReviewSummaryDto(int Count, double? Average, IReadOnlyList<AdminReviewItemDto> Recent);
public sealed record AdminComplaintPersonDto(long TelegramUserId, string FirstName, string? Username, DateTime CreatedAtUtc, bool IsBlocked, bool IsDeleted);
public sealed record AdminComplaintReporterDto(AdminComplaintPersonDto? Person, long TelegramUserId, int Filed, int Open, int Resolved, int Dismissed);
// Kind is "deal" or "offer" (D26); Status is the number the app already uses for that kind.
public sealed record AdminSharedHistoryDto(string Kind, Guid Id, string? Title, int Status, DateTime CreatedAtUtc);
public sealed record AdminComplaintReviewDto(
    AdminComplaintDto Complaint,
    long? AccusedTelegramUserId,
    AdminUserDetailsDto? Accused,
    AdminReviewSummaryDto Reviews,
    IReadOnlyList<AdminComplaintDto> OtherComplaints,
    AdminComplaintReporterDto Reporter,
    IReadOnlyList<AdminSharedHistoryDto> SharedHistory);

public sealed record GetAdminComplaintReviewQuery(long ActorTelegramUserId, Guid ComplaintId) : IRequest<AdminComplaintReviewDto>;

public sealed class GetAdminComplaintReviewHandler(
    IAdminAccessPolicy access,
    IComplaintRepository complaints,
    IAdminComplaintReviewReadModel review,
    IAdminMarketplaceReadModel marketplace) : IRequestHandler<GetAdminComplaintReviewQuery, AdminComplaintReviewDto>
{
    private const int OtherComplaintsLimit = 50;

    public async Task<AdminComplaintReviewDto> Handle(GetAdminComplaintReviewQuery query, CancellationToken cancellationToken)
    {
        access.EnsureAllowed(query.ActorTelegramUserId);
        var complaint = await complaints.GetForAdminAsync(query.ComplaintId, cancellationToken)
            ?? throw new InvalidOperationException("Complaint was not found.");
        var accusedId = complaint.OwnerTelegramUserId
            ?? await review.GetOtherSideAsync(complaint.TargetType, complaint.TargetId, complaint.ReporterTelegramUserId, cancellationToken);

        AdminUserDetailsDto? accused = null;
        var reviews = new AdminReviewSummaryDto(0, null, []);
        IReadOnlyList<AdminComplaintDto> others = [];
        IReadOnlyList<AdminSharedHistoryDto> shared = [];
        if (accusedId is { } accusedTelegramUserId)
        {
            accused = await marketplace.GetUserDetailsAsync(accusedTelegramUserId, cancellationToken);
            reviews = await review.GetReviewsAsync(accusedTelegramUserId, cancellationToken);
            var targets = await review.GetComplaintTargetIdsAsync(accusedTelegramUserId, cancellationToken);
            others = targets.Count == 0 ? [] : (await complaints.ListForAdminByTargetsAsync(targets, OtherComplaintsLimit + 1, cancellationToken))
                .Where(item => item.Id != complaint.Id).Take(OtherComplaintsLimit).ToArray();
            shared = await review.GetSharedHistoryAsync(complaint.ReporterTelegramUserId, accusedTelegramUserId, cancellationToken);
        }

        var counts = await complaints.CountByReporterAsync(complaint.ReporterTelegramUserId, cancellationToken);
        int Count(string status) => counts.TryGetValue(status, out var value) ? value : 0;
        var reporter = new AdminComplaintReporterDto(
            await review.GetPersonAsync(complaint.ReporterTelegramUserId, cancellationToken),
            complaint.ReporterTelegramUserId,
            counts.Values.Sum(),
            Count(ComplaintStatuses.Open),
            Count(ComplaintStatuses.Resolved),
            Count(ComplaintStatuses.Dismissed));
        return new AdminComplaintReviewDto(complaint, accusedId, accused, reviews, others, reporter, shared);
    }
}
