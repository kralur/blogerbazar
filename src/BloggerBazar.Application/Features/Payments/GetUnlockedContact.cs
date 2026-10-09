using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Payments;

public sealed record GetUnlockedContactQuery(ContactTargetType TargetType, Guid TargetId, long ViewerTelegramUserId) : IRequest<ContactDetailsDto>;

public sealed record ContactDetailsDto(string? Phone, string? Email, string? Telegram, string? WebsiteUrl)
{
    public static ContactDetailsDto From(BloggerProfile blogger) => new(blogger.Phone, blogger.Email, blogger.Username, null);

    public static ContactDetailsDto From(BusinessProfile business) => new(business.Phone, business.Email, business.Username, business.WebsiteUrl);

    public static ContactDetailsDto From(BrandFaceProfile brandFace, string? verifiedPhone) => new(verifiedPhone, null, brandFace.Telegram, null);
}

public sealed class GetUnlockedContactValidator : AbstractValidator<GetUnlockedContactQuery>
{
    public GetUnlockedContactValidator()
    {
        RuleFor(query => query.TargetId).NotEmpty();
        RuleFor(query => query.TargetType).IsInEnum();
    }
}

// Personal contacts are visible only to the counterparty of a deal (D31) or to a viewer who paid
// for a legacy contact unlock. Everyone else gets the same "not found" as for a missing profile.
public sealed class GetUnlockedContactHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IPlatformUserRepository users,
    IDealRepository deals,
    IContactUnlockRepository unlocks) : IRequestHandler<GetUnlockedContactQuery, ContactDetailsDto>
{
    public async Task<ContactDetailsDto> Handle(GetUnlockedContactQuery query, CancellationToken cancellationToken)
    {
        var contact = query.TargetType switch
        {
            ContactTargetType.Blogger => await bloggers.GetByIdAsync(query.TargetId, cancellationToken) is { } blogger ? ContactDetailsDto.From(blogger) : null,
            ContactTargetType.Business => await businesses.GetByIdAsync(query.TargetId, cancellationToken) is { } business ? ContactDetailsDto.From(business) : null,
            _ => null
        } ?? throw NotFound();

        if (await unlocks.GetAsync(query.ViewerTelegramUserId, query.TargetType, query.TargetId, cancellationToken) is not null
            || await SharesDealAsync(query, cancellationToken))
        {
            return contact;
        }

        throw NotFound();
    }

    private async Task<bool> SharesDealAsync(GetUnlockedContactQuery query, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, query.ViewerTelegramUserId, cancellationToken);
        if (participant is null)
        {
            return false;
        }

        return (participant.Role, query.TargetType) switch
        {
            (MarketplaceRole.Business, ContactTargetType.Blogger) => await deals.ExistsBetweenAsync(query.TargetId, participant.ProfileId, cancellationToken),
            (MarketplaceRole.Blogger, ContactTargetType.Business) => await deals.ExistsBetweenAsync(participant.ProfileId, query.TargetId, cancellationToken),
            _ => false
        };
    }

    private static InvalidOperationException NotFound() => new("Contact target was not found.");
}
