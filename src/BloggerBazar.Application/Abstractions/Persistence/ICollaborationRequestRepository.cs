using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface ICollaborationRequestRepository
{
    Task<CollaborationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsDealAsync(Guid requestId, CancellationToken cancellationToken);
    Task AddAsync(CollaborationRequest request, CancellationToken cancellationToken);
}
