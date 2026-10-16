using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Complaints;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

// Deleted profiles, campaigns and deals stay for history (D43), so every lookup ignores the soft-delete filters.
internal sealed class AdminComplaintReviewReadModel(BloggerBazarDbContext dbContext) : IAdminComplaintReviewReadModel
{
    private const int RecentReviews = 10;
    private const int SharedHistoryLimit = 20;

    public async Task<long?> GetOtherSideAsync(string targetType, Guid targetId, long reporterTelegramUserId, CancellationToken cancellationToken)
    {
        long[] sides = targetType switch
        {
            ComplaintTargets.Deal => await dbContext.Deals.IgnoreQueryFilters().AsNoTracking().Where(deal => deal.Id == targetId)
                .Select(deal => new[] { deal.Business.TelegramUserId, deal.BloggerId != null ? deal.Blogger!.TelegramUserId : deal.BrandFace!.TelegramUserId })
                .FirstOrDefaultAsync(cancellationToken) ?? [],
            ComplaintTargets.Offer => await dbContext.CollaborationRequests.IgnoreQueryFilters().AsNoTracking().Where(offer => offer.Id == targetId)
                .Select(offer => new[] { offer.Business.TelegramUserId, offer.BloggerId != null ? offer.Blogger!.TelegramUserId : offer.BrandFace!.TelegramUserId })
                .FirstOrDefaultAsync(cancellationToken) ?? [],
            _ => []
        };
        // Only a participant can report a deal or an offer; anything else has no "other side".
        return sides.Length == 2 && sides.Contains(reporterTelegramUserId) ? sides.First(side => side != reporterTelegramUserId) : null;
    }

    public async Task<IReadOnlyList<Guid>> GetComplaintTargetIdsAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var bloggers = await dbContext.BloggerProfiles.IgnoreQueryFilters().AsNoTracking().Where(profile => profile.TelegramUserId == telegramUserId).Select(profile => profile.Id).ToListAsync(cancellationToken);
        var brandFaces = await dbContext.BrandFaceProfiles.IgnoreQueryFilters().AsNoTracking().Where(profile => profile.TelegramUserId == telegramUserId).Select(profile => profile.Id).ToListAsync(cancellationToken);
        var businesses = await dbContext.BusinessProfiles.IgnoreQueryFilters().AsNoTracking().Where(profile => profile.TelegramUserId == telegramUserId).Select(profile => profile.Id).ToListAsync(cancellationToken);
        var campaigns = await dbContext.Campaigns.IgnoreQueryFilters().AsNoTracking().Where(campaign => businesses.Contains(campaign.BusinessId)).Select(campaign => campaign.Id).ToListAsync(cancellationToken);
        return [.. bloggers, .. brandFaces, .. businesses, .. campaigns];
    }

    public async Task<AdminReviewSummaryDto> GetReviewsAsync(long telegramUserId, CancellationToken cancellationToken)
    {
        var reviews = dbContext.Reviews.IgnoreQueryFilters().AsNoTracking().Where(review =>
            (review.BloggerId != null && review.Blogger!.TelegramUserId == telegramUserId)
            || (review.BrandFaceId != null && review.BrandFace!.TelegramUserId == telegramUserId)
            || (review.BusinessId != null && review.Business!.TelegramUserId == telegramUserId));
        var count = await reviews.CountAsync(cancellationToken);
        var average = count == 0 ? (double?)null : await reviews.AverageAsync(review => (double)review.Rating, cancellationToken);
        var recent = await reviews.OrderByDescending(review => review.CreatedAtUtc).Take(RecentReviews)
            .Select(review => new AdminReviewItemDto(
                review.Rating,
                review.Comment,
                dbContext.PlatformUsers.Where(user => user.TelegramUserId == review.ReviewerTelegramUserId).Select(user => user.FirstName).FirstOrDefault(),
                review.CreatedAtUtc,
                review.PublishedAtUtc != null))
            .ToListAsync(cancellationToken);
        return new AdminReviewSummaryDto(count, average is null ? null : Math.Round(average.Value, 1), recent);
    }

    public Task<AdminComplaintPersonDto?> GetPersonAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.PlatformUsers.AsNoTracking().Where(user => user.TelegramUserId == telegramUserId)
            .Select(user => new AdminComplaintPersonDto(user.TelegramUserId, user.FirstName, user.Username, user.CreatedAtUtc, user.IsBlocked, user.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminSharedHistoryDto>> GetSharedHistoryAsync(long firstTelegramUserId, long secondTelegramUserId, CancellationToken cancellationToken)
    {
        var deals = await dbContext.Deals.IgnoreQueryFilters().AsNoTracking()
            .Where(deal => (deal.Business.TelegramUserId == firstTelegramUserId || deal.Business.TelegramUserId == secondTelegramUserId)
                && ((deal.BloggerId != null && (deal.Blogger!.TelegramUserId == firstTelegramUserId || deal.Blogger!.TelegramUserId == secondTelegramUserId))
                    || (deal.BrandFaceId != null && (deal.BrandFace!.TelegramUserId == firstTelegramUserId || deal.BrandFace!.TelegramUserId == secondTelegramUserId)))
                && (deal.BloggerId != null ? deal.Blogger!.TelegramUserId : deal.BrandFace!.TelegramUserId) != deal.Business.TelegramUserId)
            .OrderByDescending(deal => deal.CreatedAtUtc).Take(SharedHistoryLimit)
            .Select(deal => new AdminSharedHistoryDto("deal", deal.Id,
                deal.CampaignTitleSnapshot ?? (deal.CampaignApplicationId != null ? deal.CampaignApplication!.Campaign.Title : null), (int)deal.Status, deal.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        // An accepted offer became a deal and is listed there already.
        var offers = await dbContext.CollaborationRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(offer => offer.Deal == null
                && (offer.Business.TelegramUserId == firstTelegramUserId || offer.Business.TelegramUserId == secondTelegramUserId)
                && ((offer.BloggerId != null && (offer.Blogger!.TelegramUserId == firstTelegramUserId || offer.Blogger!.TelegramUserId == secondTelegramUserId))
                    || (offer.BrandFaceId != null && (offer.BrandFace!.TelegramUserId == firstTelegramUserId || offer.BrandFace!.TelegramUserId == secondTelegramUserId)))
                && (offer.BloggerId != null ? offer.Blogger!.TelegramUserId : offer.BrandFace!.TelegramUserId) != offer.Business.TelegramUserId)
            .OrderByDescending(offer => offer.CreatedAtUtc).Take(SharedHistoryLimit)
            .Select(offer => new AdminSharedHistoryDto("offer", offer.Id, null, (int)offer.Status, offer.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return deals.Concat(offers).OrderByDescending(item => item.CreatedAtUtc).Take(SharedHistoryLimit).ToArray();
    }
}
