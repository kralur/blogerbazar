using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IBloggerProfileRepository
{
    Task<BloggerProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BloggerProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BloggerProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    Task<IReadOnlyList<BloggerProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BloggerProfile>>([]);
    Task<IReadOnlyList<BloggerProfile>> SearchApprovedAsync(string? city, string? category, int skip, int take, CancellationToken cancellationToken);
    Task AddAsync(BloggerProfile profile, CancellationToken cancellationToken);
}
