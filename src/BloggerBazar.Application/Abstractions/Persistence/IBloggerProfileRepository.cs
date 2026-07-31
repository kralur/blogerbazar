using BloggerBazar.Domain.Entities;

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
