using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IBusinessProfileRepository
{
    Task<BusinessProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BusinessProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<BusinessProfile?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult<BusinessProfile?>(null);
    Task<IReadOnlyList<BusinessProfile>> GetAllAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BusinessProfile>>([]);
    Task<IReadOnlyList<BusinessProfile>> GetPendingAsync(int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BusinessProfile>>([]);
    Task AddAsync(BusinessProfile profile, CancellationToken cancellationToken);
}
