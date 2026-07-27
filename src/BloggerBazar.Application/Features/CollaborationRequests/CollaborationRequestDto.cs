using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Features.CollaborationRequests;

public sealed record CollaborationRequestDto(
    Guid Id,
    Guid BloggerId,
    string BloggerName,
    Guid BusinessId,
    string BusinessName,
    string Message,
    int Status,
    Guid? DealId,
    DateTime CreatedAtUtc)
{
    public static CollaborationRequestDto From(CollaborationRequest request) =>
        new(request.Id, request.BloggerId, request.Blogger.Name, request.BusinessId, request.Business.Name,
            request.Message, (int)request.Status, request.Deal?.Id, request.CreatedAtUtc);

    public static CollaborationRequestDto From(CollaborationRequest request, string bloggerName, string businessName) =>
        new(request.Id, request.BloggerId, bloggerName, request.BusinessId, businessName,
            request.Message, (int)request.Status, request.Deal?.Id, request.CreatedAtUtc);
}
