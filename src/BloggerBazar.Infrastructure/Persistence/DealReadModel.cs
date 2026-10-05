using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class DealReadModel(BloggerBazarDbContext dbContext) : IDealReadModel
{
    public async Task<IReadOnlyList<DealReadRow>> ListForParticipantAsync(MarketplaceRole role, Guid profileId, CancellationToken cancellationToken) =>
        await Project(dbContext.Deals.AsNoTracking()
                .Where(DealParticipantFilter.For(role, profileId))
                .OrderByDescending(deal => deal.CreatedAtUtc)
                .ThenByDescending(deal => deal.Id))
            .ToArrayAsync(cancellationToken);

    public Task<DealReadRow?> FindForParticipantAsync(Guid dealId, MarketplaceRole role, Guid profileId, CancellationToken cancellationToken) =>
        Project(dbContext.Deals.AsNoTracking()
                .Where(deal => deal.Id == dealId)
                .Where(DealParticipantFilter.For(role, profileId)))
            .SingleOrDefaultAsync(cancellationToken);

    private static IQueryable<DealReadRow> Project(IQueryable<Deal> query) =>
        query.Select(deal => new DealReadRow(
            deal.Id,
            deal.CampaignApplicationId,
            deal.CollaborationRequestId,
            deal.Status,
            deal.CreatedAtUtc,
            deal.CompletedAtUtc,
            deal.Blogger.Name,
            deal.Blogger.AvatarUrl,
            deal.Business.Name,
            deal.Business.LogoUrl,
            deal.CampaignTermsSnapshotVersion,
            deal.CampaignTitleSnapshot,
            deal.CampaignDescriptionSnapshot,
            deal.CampaignCitySnapshot,
            deal.CampaignCategoriesSnapshot,
            deal.CampaignRequirementsSnapshot,
            deal.CampaignBudgetFromSnapshot,
            deal.CampaignBudgetToSnapshot,
            deal.CampaignDeadlineSnapshot,
            deal.CampaignApplication == null ? null : deal.CampaignApplication.Campaign.Title,
            deal.CampaignApplication == null ? null : deal.CampaignApplication.Campaign.Description,
            deal.CampaignApplication == null ? null : deal.CampaignApplication.Campaign.City,
            deal.CampaignApplication == null ? null : deal.CampaignApplication.Campaign.Categories,
            deal.CampaignApplication == null ? null : deal.CampaignApplication.Campaign.Requirements,
            deal.CampaignApplication == null ? null : deal.CampaignApplication.Campaign.BudgetFrom,
            deal.CampaignApplication == null ? null : deal.CampaignApplication.Campaign.BudgetTo,
            deal.CampaignApplication == null ? null : deal.CampaignApplication.Campaign.Deadline,
            deal.Reviews.Any(review => review.TargetType == ReviewTargetType.Business),
            deal.Reviews.Any(review => review.TargetType == ReviewTargetType.Blogger),
            deal.CollaborationRequest == null ? null : deal.CollaborationRequest.Format,
            deal.CollaborationRequest == null ? null : deal.CollaborationRequest.OfferedBudget,
            deal.CollaborationRequest == null ? null : deal.CollaborationRequest.Deadline,
            deal.CollaborationRequest == null ? null : deal.CollaborationRequest.Message));
}
