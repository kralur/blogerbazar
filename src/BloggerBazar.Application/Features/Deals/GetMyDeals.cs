using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using MediatR;

namespace BloggerBazar.Application.Features.Deals;

public sealed record GetMyDealsQuery(long TelegramUserId) : IRequest<IReadOnlyList<MyDealDto>>;

public sealed record MyDealDto(
    Guid Id,
    Guid? CampaignApplicationId,
    Guid? CollaborationRequestId,
    string Title,
    string CounterpartyName,
    int Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    bool CanComplete,
    bool CanReview)
{
    public static MyDealDto From(Deal deal, long telegramUserId)
    {
        var viewerIsBlogger = deal.Blogger.TelegramUserId == telegramUserId;
        var counterpartyName = viewerIsBlogger ? deal.Business.Name : deal.Blogger.Name;
        var hasReviewed = deal.Reviews.Any(review => review.ReviewerTelegramUserId == telegramUserId);

        return new(
            deal.Id,
            deal.CampaignApplicationId,
            deal.CollaborationRequestId,
            deal.CampaignApplication?.Campaign.Title ?? "Direct collaboration request",
            counterpartyName,
            (int)deal.Status,
            deal.CreatedAtUtc,
            deal.CompletedAtUtc,
            deal.Status == DealStatus.Active,
            deal.Status == DealStatus.Completed && !hasReviewed);
    }
}

public sealed class GetMyDealsHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IMarketplaceCatalogReadModel catalog) : IRequestHandler<GetMyDealsQuery, IReadOnlyList<MyDealDto>>
{
    public async Task<IReadOnlyList<MyDealDto>> Handle(GetMyDealsQuery query, CancellationToken cancellationToken)
    {
        var blogger = await bloggers.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        var business = await businesses.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        return await catalog.GetDealsAsync(blogger?.Id, business?.Id, query.TelegramUserId, cancellationToken);
    }
}
