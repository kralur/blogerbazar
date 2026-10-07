using BloggerBazar.Application.Features.Businesses;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IPublicBusinessReadModel
{
    // Null for a deleted, unapproved or blocked business: the same rule that hides its campaigns.
    Task<PublicBusinessProfileDto?> GetAsync(Guid businessId, DateTime utcNow, CancellationToken cancellationToken);
}
