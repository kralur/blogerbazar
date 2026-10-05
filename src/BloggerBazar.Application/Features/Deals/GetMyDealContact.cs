using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Payments;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Deals;

public sealed record GetMyDealContactQuery(Guid DealId, long TelegramUserId) : IRequest<ContactDetailsDto>;

public sealed class GetMyDealContactValidator : AbstractValidator<GetMyDealContactQuery>
{
    public GetMyDealContactValidator()
    {
        RuleFor(query => query.DealId).NotEmpty();
        RuleFor(query => query.TelegramUserId).GreaterThan(0);
    }
}

public sealed class GetMyDealContactHandler(
    IDealRepository deals,
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses) : IRequestHandler<GetMyDealContactQuery, ContactDetailsDto>
{
    public async Task<ContactDetailsDto> Handle(GetMyDealContactQuery query, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, query.TelegramUserId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        var deal = await deals.GetForParticipantAsync(query.DealId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        return participant.Role == MarketplaceRole.Blogger ? ContactDetailsDto.From(deal.Business) : ContactDetailsDto.From(deal.Blogger);
    }
}
