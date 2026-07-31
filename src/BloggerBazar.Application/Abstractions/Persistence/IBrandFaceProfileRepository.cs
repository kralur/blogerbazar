using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IBrandFaceProfileRepository
{
    Task<BrandFaceProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BrandFaceProfile?> GetByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken);
    Task<BrandFaceProfile?> GetIncludingDeletedByTelegramUserIdAsync(long telegramUserId, CancellationToken cancellationToken) => GetByTelegramUserIdAsync(telegramUserId, cancellationToken);
    Task<IReadOnlyList<BrandFaceProfile>> GetAllAsync(int take, CancellationToken cancellationToken);
    Task<IReadOnlyList<BrandFaceProfile>> SearchAsync(string? query, string? city, string? category, int skip, int take, CancellationToken cancellationToken);
    Task AddAsync(BrandFaceProfile profile, CancellationToken cancellationToken);
}
