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
    IBusinessProfileRepository businesses,
    IBrandFaceProfileRepository? brandFaces = null) : IRequestHandler<GetMyDealContactQuery, ContactDetailsDto>
{
    public async Task<ContactDetailsDto> Handle(GetMyDealContactQuery query, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, query.TelegramUserId, cancellationToken, brandFaces)
            ?? throw DealAccess.DealNotFound();
        var deal = await deals.GetForParticipantAsync(query.DealId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        if (participant.IsCreator)
        {
            return ContactDetailsDto.From(deal.Business);
        }

        if (deal.Blogger is not null)
        {
            return ContactDetailsDto.From(deal.Blogger);
        }

        // A brand face profile keeps no phone of its own: the number is the one Telegram verified for the account.
        var brandFace = deal.BrandFace ?? throw DealAccess.DealNotFound();
        var account = await users.GetByTelegramUserIdAsync(brandFace.TelegramUserId, cancellationToken);
        return ContactDetailsDto.From(brandFace, account?.VerifiedPhone);
    }
}
