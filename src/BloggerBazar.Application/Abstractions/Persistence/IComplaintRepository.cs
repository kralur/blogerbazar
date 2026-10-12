using BloggerBazar.Application.Features.Complaints;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IComplaintRepository
{
    Task<Complaint?> FindOpenAsync(long reporterTelegramUserId, string targetType, Guid targetId, CancellationToken cancellationToken);
    Task<int> CountSinceAsync(long reporterTelegramUserId, DateTime sinceUtc, CancellationToken cancellationToken);
    Task<Complaint?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Complaint complaint, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminComplaintDto>> ListForAdminAsync(string? status, int take, CancellationToken cancellationToken);
    Task<int> CountOpenAsync(CancellationToken cancellationToken);
}
