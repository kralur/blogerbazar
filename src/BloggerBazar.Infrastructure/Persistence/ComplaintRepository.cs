using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Complaints;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class ComplaintRepository(BloggerBazarDbContext dbContext) : IComplaintRepository
{
    public Task<Complaint?> FindOpenAsync(long reporterTelegramUserId, string targetType, Guid targetId, CancellationToken cancellationToken) =>
        dbContext.Complaints.AsNoTracking().SingleOrDefaultAsync(complaint => complaint.ReporterTelegramUserId == reporterTelegramUserId
            && complaint.TargetType == targetType && complaint.TargetId == targetId && complaint.Status == Complaint.OpenStatus, cancellationToken);

    public Task<int> CountSinceAsync(long reporterTelegramUserId, DateTime sinceUtc, CancellationToken cancellationToken) =>
        dbContext.Complaints.CountAsync(complaint => complaint.ReporterTelegramUserId == reporterTelegramUserId && complaint.CreatedAtUtc >= sinceUtc, cancellationToken);

    public Task<Complaint?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Complaints.SingleOrDefaultAsync(complaint => complaint.Id == id, cancellationToken);

    public async Task AddAsync(Complaint complaint, CancellationToken cancellationToken) => await dbContext.Complaints.AddAsync(complaint, cancellationToken);

    public Task<int> CountOpenAsync(CancellationToken cancellationToken) =>
        dbContext.Complaints.CountAsync(complaint => complaint.Status == Complaint.OpenStatus, cancellationToken);

    // The admin sees what was reported by name, newest first; offers and deals show both sides.
    public async Task<IReadOnlyList<AdminComplaintDto>> ListForAdminAsync(string? status, int take, CancellationToken cancellationToken)
    {
        var items = await dbContext.Complaints.AsNoTracking()
            .Where(complaint => status == null || complaint.Status == status)
            .OrderByDescending(complaint => complaint.CreatedAtUtc)
            .ThenByDescending(complaint => complaint.Id)
            .Take(take)
            .Select(complaint => new AdminComplaintDto(
                complaint.Id,
                complaint.TargetType,
                complaint.TargetId,
                complaint.TargetType == ComplaintTargets.Blogger ? dbContext.BloggerProfiles.IgnoreQueryFilters().Where(profile => profile.Id == complaint.TargetId).Select(profile => profile.Name).FirstOrDefault()
                    : complaint.TargetType == ComplaintTargets.BrandFace ? dbContext.BrandFaceProfiles.IgnoreQueryFilters().Where(profile => profile.Id == complaint.TargetId).Select(profile => profile.Name).FirstOrDefault()
                    : complaint.TargetType == ComplaintTargets.Business ? dbContext.BusinessProfiles.IgnoreQueryFilters().Where(profile => profile.Id == complaint.TargetId).Select(profile => profile.Name).FirstOrDefault()
                    : complaint.TargetType == ComplaintTargets.Campaign ? dbContext.Campaigns.IgnoreQueryFilters().Where(campaign => campaign.Id == complaint.TargetId).Select(campaign => campaign.Title).FirstOrDefault()
                    : complaint.TargetType == ComplaintTargets.Deal ? dbContext.Deals.IgnoreQueryFilters().Where(deal => deal.Id == complaint.TargetId)
                        .Select(deal => (deal.BloggerId != null ? deal.Blogger!.Name : deal.BrandFace!.Name) + " · " + deal.Business.Name).FirstOrDefault()
                    : complaint.TargetType == ComplaintTargets.Offer ? dbContext.CollaborationRequests.IgnoreQueryFilters().Where(offer => offer.Id == complaint.TargetId)
                        .Select(offer => (offer.BloggerId != null ? offer.Blogger!.Name : offer.BrandFace!.Name) + " · " + offer.Business.Name).FirstOrDefault()
                    : null,
                complaint.Reason,
                complaint.Comment,
                complaint.Status,
                complaint.ReporterTelegramUserId,
                dbContext.PlatformUsers.Where(user => user.TelegramUserId == complaint.ReporterTelegramUserId).Select(user => user.FirstName).FirstOrDefault(),
                (int)complaint.ReporterRole,
                complaint.CreatedAtUtc,
                complaint.ResolvedAtUtc,
                complaint.ResolutionNote,
                complaint.TargetType == ComplaintTargets.Blogger ? dbContext.BloggerProfiles.IgnoreQueryFilters().Where(profile => profile.Id == complaint.TargetId).Select(profile => (long?)profile.TelegramUserId).FirstOrDefault()
                    : complaint.TargetType == ComplaintTargets.BrandFace ? dbContext.BrandFaceProfiles.IgnoreQueryFilters().Where(profile => profile.Id == complaint.TargetId).Select(profile => (long?)profile.TelegramUserId).FirstOrDefault()
                    : complaint.TargetType == ComplaintTargets.Business ? dbContext.BusinessProfiles.IgnoreQueryFilters().Where(profile => profile.Id == complaint.TargetId).Select(profile => (long?)profile.TelegramUserId).FirstOrDefault()
                    : complaint.TargetType == ComplaintTargets.Campaign ? dbContext.BusinessProfiles.IgnoreQueryFilters()
                        .Where(profile => dbContext.Campaigns.IgnoreQueryFilters().Any(campaign => campaign.Id == complaint.TargetId && campaign.BusinessId == profile.Id))
                        .Select(profile => (long?)profile.TelegramUserId).FirstOrDefault()
                    : null,
                false))
            .ToArrayAsync(cancellationToken);

        var ownerIds = items.Where(item => item.OwnerTelegramUserId != null).Select(item => item.OwnerTelegramUserId!.Value).Distinct().ToArray();
        if (ownerIds.Length == 0)
        {
            return items;
        }

        var blocked = (await dbContext.PlatformUsers.AsNoTracking()
            .Where(user => ownerIds.Contains(user.TelegramUserId) && user.IsBlocked)
            .Select(user => user.TelegramUserId)
            .ToArrayAsync(cancellationToken)).ToHashSet();
        return items.Select(item => item.OwnerTelegramUserId is { } owner && blocked.Contains(owner) ? item with { OwnerIsBlocked = true } : item).ToArray();
    }
}
