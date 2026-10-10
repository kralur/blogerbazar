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
    DateTime? ReviewDeadlineUtc = null,
    Guid? CounterpartyProfileId = null,
    bool PartnerHasReviewed = false,
    bool CounterpartyDeleted = false,
    string? CounterpartyRole = null,
    int? AgreedPrice = null,
    bool CanSetPrice = false,
    IReadOnlyList<DealPublicationDto>? Publications = null,
    bool CanAddPublication = false,
    bool CanConfirmPublications = false)
{
    internal static DealDetailsDto From(DealReadRow row, MarketplaceRole viewerRole, IReadOnlyList<DealPublicationDto>? publications = null)
    {
        var view = DealView.From(row, viewerRole);
        // D51: an offer's budget is the agreed price; otherwise the business may enter one. Results close with the deal's partner.
        var resultsOpen = !view.CounterpartyDeleted && row.Status != DealStatus.Cancelled;
        var viewerIsBusiness = viewerRole == MarketplaceRole.Business;
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
            view.ReviewDeadlineUtc,
            view.CounterpartyProfileId,
            view.PartnerHasReviewed,
            view.CounterpartyDeleted,
            view.CounterpartyRole,
            row.OfferedBudget ?? row.AgreedPrice,
            resultsOpen && viewerIsBusiness && row.OfferedBudget is null,
            publications ?? [],
            resultsOpen && !viewerIsBusiness,
            resultsOpen && viewerIsBusiness);
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
    IDealReadModel deals,
    IBrandFaceProfileRepository? brandFaces = null,
    IDealPublicationRepository? publications = null) : IRequestHandler<GetMyDealQuery, DealDetailsDto>
{
    public async Task<DealDetailsDto> Handle(GetMyDealQuery query, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, query.TelegramUserId, cancellationToken, brandFaces)
            ?? throw DealAccess.DealNotFound();
        var row = await deals.FindForParticipantAsync(query.DealId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        var items = publications is null ? [] : (await publications.ListForDealAsync(row.Id, cancellationToken)).Select(DealPublicationDto.From).ToArray();
        return DealDetailsDto.From(row, participant.Role, items);
    }
}
