using BloggerBazar.Application.Features.Admin;

namespace BloggerBazar.Application.Abstractions.Persistence;

// D60: read-only queries behind the admin "Suspicious" page; every list is capped.
public interface IAdminSignalsReadModel
{
    Task<IReadOnlyList<AdminDuplicateHandleDto>> GetDuplicateHandlesAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSharedPhoneDto>> GetSharedPhonesAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminQuickDealDto>> GetQuickDealsAsync(DateTime sinceUtc, int maxMinutes, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminComplaintTargetDto>> GetComplaintTargetsAsync(DateTime sinceUtc, int minReporters, int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSignupBurstDto>> GetSignupBurstsAsync(DateTime sinceUtc, int minAccounts, int take, CancellationToken cancellationToken);
}
