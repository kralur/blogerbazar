using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

internal sealed class AuditLogRepository(BloggerBazarDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry, CancellationToken cancellationToken) =>
        await dbContext.AuditLogs.AddAsync(entry, cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken) =>
        // The admin journal shows what matters for safety (roles, blocks, deletions, complaints, promotions); favorites are noise there.
        await dbContext.AuditLogs.AsNoTracking()
            .Where(entry => !entry.Action.StartsWith("favorite.") && !entry.Action.StartsWith("brand-face-favorite."))
            .OrderByDescending(entry => entry.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);
}
