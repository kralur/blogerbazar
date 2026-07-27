using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICollaborationRequestRepository
{
    Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CollaborationRequest>> GetForBloggerAsync(Guid bloggerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CollaborationRequest>> GetForBusinessAsync(Guid businessId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CollaborationRequest>> GetAllAsync(int take, CancellationToken cancellationToken);
    Task AddAsync(CollaborationRequest request, CancellationToken cancellationToken);
}
