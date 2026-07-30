using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class BrandFaceProfileRepository(BloggerBazarDbContext dbContext) : IBrandFaceProfileRepository
{
    public Task<BrandFaceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BrandFaceProfiles.AsNoTracking().SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public Task<BrandFaceProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) =>
        dbContext.BrandFaceProfiles.SingleOrDefaultAsync(profile => profile.TelegramUserId == telegramUserId, cancellationToken);

    public async Task<IReadOnlyList<BrandFaceProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        await dbContext.BrandFaceProfiles.AsNoTracking().OrderByDescending(profile => profile.IsPromoted).ThenByDescending(profile => profile.UpdatedAtUtc).Take(take).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BrandFaceProfile>> SearchAsync(string? query, string? city, string? category, int skip, int take, CancellationToken cancellationToken)
    {
        var profiles = dbContext.BrandFaceProfiles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var value = query.Trim().ToLowerInvariant();
            profiles = profiles.Where(profile => profile.Name.ToLower().Contains(value) || profile.City.ToLower().Contains(value) || profile.Categories.Any(categoryValue => categoryValue.ToLower().Contains(value)));
        }
        if (!string.IsNullOrWhiteSpace(city)) profiles = profiles.Where(profile => profile.City == city.Trim());
        if (!string.IsNullOrWhiteSpace(category)) profiles = profiles.Where(profile => profile.Categories.Contains(category.Trim()));
        return await profiles.OrderByDescending(profile => profile.IsPromoted).ThenByDescending(profile => profile.UpdatedAtUtc).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BrandFaceProfile profile, CancellationToken cancellationToken) =>
        await dbContext.BrandFaceProfiles.AddAsync(profile, cancellationToken);
}
