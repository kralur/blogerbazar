using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CampaignApplicationRepository(BloggerBazarDbContext dbContext) : ICampaignApplicationRepository
{
    public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CampaignApplications
            .Include(application => application.Campaign)
            .SingleOrDefaultAsync(application => application.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid campaignId, MarketplaceRole creatorRole, Guid creatorId, CancellationToken cancellationToken) =>
        creatorRole == MarketplaceRole.BrandFace
            ? dbContext.CampaignApplications.AnyAsync(application => application.CampaignId == campaignId && application.BrandFaceId == creatorId, cancellationToken)
            : dbContext.CampaignApplications.AnyAsync(application => application.CampaignId == campaignId && application.BloggerId == creatorId, cancellationToken);

    public async Task AddAsync(CampaignApplication application, CancellationToken cancellationToken) => await dbContext.CampaignApplications.AddAsync(application, cancellationToken);
}
