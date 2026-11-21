using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICampaignRepository
{
    Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Campaign?> GetByIdForBusinessAsync(Guid id, Guid businessId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Campaign>> SearchPublishedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<Campaign>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Campaign>>([]);
    Task AddAsync(Campaign campaign, CancellationToken cancellationToken);
    Task<int> CountCreatedSinceAsync(Guid businessId, DateTime sinceUtc, CancellationToken cancellationToken) => Task.FromResult(0);
    // Deletes the owner's campaign only while it has no applications; false when nothing was deleted.
    Task<bool> DeleteWithoutApplicationsAsync(Guid id, Guid businessId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    // D69: drafts and closed campaigns whose planned publication time has come, oldest first.
    Task<IReadOnlyList<DueScheduledCampaign>> ListDueScheduledAsync(DateTime nowUtc, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DueScheduledCampaign>>([]);

    // Publishes only while the plan is unchanged (same time, still a draft or closed) and the owner's account is still active;
    // false when the owner changed it first or the account was blocked meanwhile.
    Task<bool> TryPublishScheduledAsync(Guid id, DateTime scheduledAtUtc, DateTime nowUtc, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    // Drops a plan that can no longer run, only while it is unchanged; false when the owner changed it first.
    Task<bool> TryDropScheduleAsync(Guid id, DateTime scheduledAtUtc, DateTime nowUtc, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}

// CompanyActive: the company owner's account is neither blocked nor deleted (a blocked account cannot publish by hand either).
public sealed record DueScheduledCampaign(Guid Id, string Title, DateTime ScheduledPublishAtUtc, DateTime? Deadline, long OwnerTelegramUserId, bool CompanyActive = true);
