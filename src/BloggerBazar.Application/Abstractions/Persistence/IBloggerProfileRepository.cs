using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Application.Abstractions.Persistence;

public sealed record BloggerCatalogSearch(
    string? Query,
    string? City,
    string? Category,
    string? Platform,
    int? MinFollowers,
    int? MinEngagementRate,
    int? MaxEngagementRate,
    int? MinPrice,
    int? MaxPrice,
    string? Sort,
    int Page,
    int PageSize);

public sealed record BloggerCatalogPage(IReadOnlyList<BloggerProfile> Profiles, int Total);

public interface IBloggerProfileRepository
{
    Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    // Audit 2026-10-22: moves a profile out of "pending" only if it is still pending (conditional update), so two
    // moderators deciding at once cannot both win; false when someone else already decided.
    Task<bool> TryDecidePendingAsync(Guid id, BloggerStatus status, CancellationToken cancellationToken) => Task.FromResult(true);
    Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<BloggerProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
    Task<BloggerProfile?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult<BloggerProfile?>(null);
    Task<IReadOnlyList<BloggerProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    Task<IReadOnlyList<BloggerProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken);
    Task<BloggerCatalogPage> SearchApprovedPageAsync(BloggerCatalogSearch search, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This repository does not support catalog search.");
    Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken);
}
