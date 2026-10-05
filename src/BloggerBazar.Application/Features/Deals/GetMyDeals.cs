using BloggerBazar.Application.Abstractions.Persistence;
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
    string? CounterpartyImageUrl,
    int Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    bool CanComplete,
    bool CanReview,
    string SourceType,
    string TermsSource)
{
    // Kept for backward compatibility of the legacy list; clients should use SourceType.
    internal const string LegacyCollaborationTitle = "Direct collaboration request";

    internal static MyDealDto From(DealReadRow row, MarketplaceRole viewerRole)
    {
        var view = DealView.From(row, viewerRole);
        return new(
            row.Id,
            row.CampaignApplicationId,
            row.CollaborationRequestId,
            view.Terms?.Title ?? LegacyCollaborationTitle,
            view.CounterpartyName,
            view.CounterpartyImageUrl,
            (int)row.Status,
            row.CreatedAtUtc,
            row.CompletedAtUtc,
            view.CanComplete,
            view.CanReview,
            view.SourceType,
            view.TermsSource);
    }
}

public sealed class GetMyDealsHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IDealReadModel deals) : IRequestHandler<GetMyDealsQuery, IReadOnlyList<MyDealDto>>
{
    public async Task<IReadOnlyList<MyDealDto>> Handle(GetMyDealsQuery query, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, query.TelegramUserId, cancellationToken);
        if (participant is null)
        {
            return [];
        }

        var rows = await deals.ListForParticipantAsync(participant.Role, participant.ProfileId, cancellationToken);
        return rows.Select(row => MyDealDto.From(row, participant.Role)).ToArray();
    }
}
