using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class CampaignApplicationRepository(BloggerBazarDbContext dbContext) : ICampaignApplicationRepository
{
    public Task<CampaignApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CampaignApplications.Include(application => application.Campaign).SingleOrDefaultAsync(application => application.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid campaignId, Guid bloggerId, CancellationToken cancellationToken) =>
        dbContext.CampaignApplications.AnyAsync(application => application.CampaignId == campaignId && application.BloggerId == bloggerId, cancellationToken);

    public async Task AddAsync(CampaignApplication application, CancellationToken cancellationToken) => await dbContext.CampaignApplications.AddAsync(application, cancellationToken);
}
