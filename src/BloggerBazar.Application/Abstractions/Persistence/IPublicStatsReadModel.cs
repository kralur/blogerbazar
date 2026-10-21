using BloggerBazar.Application.Features.PublicStats;

namespace BloggerBazar.Application.Abstractions.Persistence;

public interface IPublicStatsReadModel
{
    Task<PublicStatsDto> GetAsync(CancellationToken cancellationToken);
}
