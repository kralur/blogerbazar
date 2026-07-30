using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;

namespace BloggerBazar.Application.Features.CollaborationRequests;

public sealed record GetMyCollaborationRequestsQuery(long TelegramUserId) : IRequest<IReadOnlyList<CollaborationRequestDto>>;

public sealed class GetMyCollaborationRequestsHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IMarketplaceCatalogReadModel catalog) : IRequestHandler<GetMyCollaborationRequestsQuery, IReadOnlyList<CollaborationRequestDto>>
{
    public async Task<IReadOnlyList<CollaborationRequestDto>> Handle(GetMyCollaborationRequestsQuery query, CancellationToken cancellationToken)
    {
        var blogger = await bloggers.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        var business = await businesses.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        return await catalog.GetCollaborationRequestsAsync(blogger?.Id, business?.Id, 100, cancellationToken);
    }
}
