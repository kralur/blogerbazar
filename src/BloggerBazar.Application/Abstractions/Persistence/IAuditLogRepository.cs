using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken);
}
