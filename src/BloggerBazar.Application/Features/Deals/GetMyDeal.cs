using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Deals;

public sealed record GetMyDealQuery(Guid DealId, long TelegramUserId) : IRequest<DealDetailsDto>;

public sealed record DealDetailsDto(
    Guid Id,
    int Status,
    string SourceType,
    string TermsSource,
    DealTermsDto? Terms,
    string CounterpartyName,
    string? CounterpartyImageUrl,
    Guid? CampaignApplicationId,
    Guid? CollaborationRequestId,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    bool CanComplete,
    bool CanReview,
    bool HasReviewed,
    DealOfferDto? Offer = null,
    DateTime? ReviewDeadlineUtc = null)
{
    internal static DealDetailsDto From(DealReadRow row, MarketplaceRole viewerRole)
    {
        var view = DealView.From(row, viewerRole);
        return new(
            row.Id,
            (int)row.Status,
            view.SourceType,
            view.TermsSource,
            view.Terms,
            view.CounterpartyName,
            view.CounterpartyImageUrl,
            row.CampaignApplicationId,
            row.CollaborationRequestId,
            row.CreatedAtUtc,
            row.CompletedAtUtc,
            view.CanComplete,
            view.CanReview,
            view.HasReviewed,
            view.Offer,
            view.ReviewDeadlineUtc);
    }
}

public sealed class GetMyDealValidator : AbstractValidator<GetMyDealQuery>
{
    public GetMyDealValidator()
    {
        RuleFor(query => query.DealId).NotEmpty();
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
    }
}

public sealed class GetMyDealHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IDealReadModel deals) : IRequestHandler<GetMyDealQuery, DealDetailsDto>
{
    public async Task<DealDetailsDto> Handle(GetMyDealQuery query, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, query.TelegramUserId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        var row = await deals.FindForParticipantAsync(query.DealId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        return DealDetailsDto.From(row, participant.Role);
    }
}
