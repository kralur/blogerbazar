using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class DealReminderRepository(BloggerBazarDbContext dbContext) : IDealReminderRepository
{
    private const int MaxCandidatesPerRun = 500;

    public async Task<IReadOnlyList<DealReminderCandidate>> GetCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var staleCreatedBefore = nowUtc.AddDays(-7);
        var reviewWindowStart = nowUtc - ReviewWindow.Length;
        var firstReviewReminderAt = nowUtc.AddDays(-1);

        // IgnoreQueryFilters: hidden reviews still mean the side has already reviewed.
        return await dbContext.Deals.AsNoTracking().IgnoreQueryFilters()
            .Where(deal => !deal.Blogger.IsDeleted && !deal.Business.IsDeleted)
            .Where(deal =>
                (deal.Status == DealStatus.Active && deal.CreatedAtUtc <= staleCreatedBefore
                    && dbContext.DealReminders.Count(reminder => reminder.DealId == deal.Id && reminder.Kind == DealReminderKind.CompleteDay14) < 2)
                || (deal.Status == DealStatus.Completed && deal.CompletedAtUtc > reviewWindowStart && deal.CompletedAtUtc <= firstReviewReminderAt
                    && deal.Reviews.Count() < 2))
            .OrderBy(deal => deal.CreatedAtUtc)
            .ThenBy(deal => deal.Id)
            .Take(MaxCandidatesPerRun)
            .Select(deal => new DealReminderCandidate(
                deal.Id,
                deal.Status,
                deal.CreatedAtUtc,
                deal.CompletedAtUtc,
                deal.Blogger.TelegramUserId,
                deal.Business.TelegramUserId,
                deal.Reviews.Any(review => review.TargetType == ReviewTargetType.Business),
                deal.Reviews.Any(review => review.TargetType == ReviewTargetType.Blogger),
                deal.Blogger.Name,
                deal.Business.Name,
                deal.CampaignTitleSnapshot,
                deal.CollaborationRequest != null ? deal.CollaborationRequest.Format : null))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> TryClaimAsync(Guid dealId, DealReminderKind kind, MarketplaceRole recipientRole, DateTime nowUtc, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO deal_reminders ("Id", "DealId", "Kind", "RecipientRole", "SentAtUtc")
            VALUES ({Guid.NewGuid()}, {dealId}, {(int)kind}, {(int)recipientRole}, {nowUtc})
            ON CONFLICT ("DealId", "Kind", "RecipientRole") DO NOTHING
            """, cancellationToken) == 1;
}
