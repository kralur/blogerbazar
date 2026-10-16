using BloggerBazar.Application.Features.Admin;

namespace BloggerBazar.Application.Abstractions.Persistence;

// Web admin analytics, grouped in the database (D57, phase 2). Days and months are Tashkent calendar days.
public interface IAdminAnalyticsReadModel
{
    // Only the days that have at least one event; the handler fills the rest with zeros.
    Task<IReadOnlyList<AdminDailyPointDto>> GetDailyAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
    // Only the months that have completed deals, from the first day of firstMonth.
    Task<IReadOnlyList<AdminMoneyMonthDto>> GetMoneyAsync(DateOnly firstMonth, CancellationToken cancellationToken);
    Task<AdminGeographyDto> GetGeographyAsync(CancellationToken cancellationToken);
    Task<AdminRetentionDto> GetRetentionAsync(DateOnly from, DateOnly to, DateTime utcNow, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminSourceRowDto>> GetSourcesAsync(CancellationToken cancellationToken);
}
