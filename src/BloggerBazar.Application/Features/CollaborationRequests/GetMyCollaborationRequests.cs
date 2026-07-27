using BloggerBazar.Application.Abstractions.Persistence;
using MediatR;

namespace BloggerBazar.Application.Features.CollaborationRequests;

public sealed record GetMyCollaborationRequestsQuery(long TelegramUserId) : IRequest<IReadOnlyList<CollaborationRequestDto>>;

public sealed class GetMyCollaborationRequestsHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICollaborationRequestRepository requests) : IRequestHandler<GetMyCollaborationRequestsQuery, IReadOnlyList<CollaborationRequestDto>>
{
    public async Task<IReadOnlyList<CollaborationRequestDto>> Handle(GetMyCollaborationRequestsQuery query, CancellationToken cancellationToken)
    {
        var blogger = await bloggers.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        var business = await businesses.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        var result = new List<CollaborationRequestDto>();
        if (blogger is not null) result.AddRange((await requests.GetForBloggerAsync(blogger.Id, cancellationToken)).Select(CollaborationRequestDto.From));
        if (business is not null) result.AddRange((await requests.GetForBusinessAsync(business.Id, cancellationToken)).Select(CollaborationRequestDto.From));
        return result.OrderByDescending(request => request.CreatedAtUtc).ToArray();
    }
}
