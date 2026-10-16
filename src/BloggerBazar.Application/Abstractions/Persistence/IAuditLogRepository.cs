using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken);
    // Newest first within [fromUtc, toUtc); either end may be open.
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken) =>
        GetRecentAsync(take, cancellationToken);
}
