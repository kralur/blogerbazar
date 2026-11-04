using BloggerBazar.Application.Features.Complaints;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IComplaintRepository
{
    Task<Complaint?> FindOpenAsync(long reporterTelegramUserId, string targetType, Guid targetId, CancellationToken cancellationToken);
    Task<int> CountSinceAsync(long reporterTelegramUserId, DateTime sinceUtc, CancellationToken cancellationToken);
    Task<Complaint?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    // Audit 2026-10-22: closes only a complaint that is still open (conditional update), so two admins closing it at
    // the same moment cannot both win; false when someone else already closed it.
    Task<bool> TryCloseOpenAsync(Guid id, string status, CancellationToken cancellationToken) => Task.FromResult(true);
    Task AddAsync(Complaint complaint, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminComplaintDto>> ListForAdminAsync(string? status, int take, CancellationToken cancellationToken);
    Task<int> CountOpenAsync(CancellationToken cancellationToken);
    // The complaint review in the web admin: one case, the cases against the same profiles or campaigns, and how many
    // cases a reporter filed per status.
    Task<AdminComplaintDto?> GetForAdminAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<AdminComplaintDto?>(null);
    Task<IReadOnlyList<AdminComplaintDto>> ListForAdminByTargetsAsync(IReadOnlyCollection<Guid> targetIds, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AdminComplaintDto>>([]);
    Task<IReadOnlyDictionary<string, int>> CountByReporterAsync(long reporterTelegramUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
}
